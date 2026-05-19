using System.Text.Json;
using System.Globalization;
using System.Collections.Concurrent;
using CsvHelper;
using CsvHelper.Configuration;

var builder = WebApplication.CreateBuilder(args);
// Register InatService as a typed HttpClient so it receives a configured HttpClient
builder.Services.AddHttpClient<aviansync.Services.InatService>();
builder.Services.AddHttpClient<aviansync.Services.EbirdTaxonomyService>();

var app = builder.Build();

var jobs     = new ConcurrentDictionary<string, string>(); // jobId -> output file path
var progress = new ConcurrentDictionary<string, string>(); // jobId -> serialized progress JSON
var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "uploads");
Directory.CreateDirectory(uploadsDir);

// Clear any files left over from a previous run
foreach (var f in Directory.GetFiles(uploadsDir)) try { File.Delete(f); } catch { }

// Helper: write progress in-memory (no file I/O race condition)
void WriteProgress(string jobId, object payload)
{
	progress[jobId] = System.Text.Json.JsonSerializer.Serialize(payload);
}

// Helper: read progress from memory
object? ReadProgress(string jobId)
{
	if (!progress.TryGetValue(jobId, out var json)) return null;
	return System.Text.Json.JsonSerializer.Deserialize<JsonElement>(json);
}

// Serve HTML UI at root
app.MapGet("/", () =>
{
	var html = @"
<!DOCTYPE html>
<html lang=""en"">
<head>
	<meta charset=""utf-8"">
	<meta name=""viewport"" content=""width=device-width, initial-scale=1"">
	<title>aviansync</title>
	<link rel=""icon"" type=""image/svg+xml"" href=""/favicon.svg"">
	<link href=""https://cdn.jsdelivr.net/npm/bootstrap@5.3.3/dist/css/bootstrap.min.css"" rel=""stylesheet"">
	<style>
		:root { --green: #74ac00; --green-dark: #5d8a00; }
		body { background: #f5f7f2; }
		.brand { color: var(--green); font-weight: 700; letter-spacing: -0.5px; }
		.btn-primary { background: var(--green); border-color: var(--green); }
		.btn-primary:hover, .btn-primary:focus { background: var(--green-dark); border-color: var(--green-dark); }
		.progress-bar { background: var(--green); }
		a { color: var(--green); }
		a:hover { color: var(--green-dark); }
		th { cursor: pointer; user-select: none; white-space: nowrap; }
		th[data-dir=asc]::after  { content: ' \25B2'; font-size: 0.7em; opacity: 0.6; }
		th[data-dir=desc]::after { content: ' \25BC'; font-size: 0.7em; opacity: 0.6; }
		.table td { vertical-align: middle; }
		.badge-yes     { background: #d4edda; color: #155724; }
		.badge-no      { background: #f8d7da; color: #721c24; }
		.badge-invalid { background: #e2e3e5; color: #383d41; }
	</style>
</head>
<body>
<div class=""container py-5"" style=""max-width:680px"">

	<h1 class=""brand mb-0"">aviansync</h1>
	<p class=""text-muted mb-4"">Migrate your <a href=""https://www.inaturalist.org"" target=""_blank"">iNaturalist</a> bird observations to <a href=""https://ebird.org"" target=""_blank"">eBird</a>.</p>

	<div class=""card shadow-sm mb-4"">
		<div class=""card-body"">
			<form id=""uploadForm"" enctype=""multipart/form-data"">
				<div class=""mb-3"">
					<label for=""user_id"" class=""form-label fw-semibold"">iNaturalist user ID</label>
					<input type=""text"" class=""form-control"" id=""user_id"" name=""user_id"" required>
				</div>
				<div class=""mb-3"">
					<label for=""file"" class=""form-label fw-semibold"">
						eBird data export
						<a href=""https://ebird.org/downloadMyData"" target=""_blank"" class=""fw-normal small ms-1"">download MyEBirdData.csv</a>
					</label>
					<input type=""file"" class=""form-control"" id=""file"" name=""file"" accept="".csv"" required>
				</div>
				<div class=""mb-3"">
					<label for=""ebird_api_key"" class=""form-label fw-semibold"">
						eBird API key
						<span class=""fw-normal text-muted small ms-1"">optional &mdash; enables taxonomy validation</span>
					</label>
					<input type=""text"" class=""form-control"" id=""ebird_api_key"" name=""ebird_api_key"" placeholder=""Get yours at ebird.org/api/keygen"">
				</div>
				<div class=""mb-2"">
					<div class=""form-check"">
						<input class=""form-check-input"" type=""checkbox"" id=""display_all"" name=""display_all"">
						<label class=""form-check-label"" for=""display_all"">Include species already in eBird</label>
						<div class=""form-text"">If unchecked, only species missing from your eBird life list are exported.</div>
					</div>
				</div>
				<div class=""mb-4"">
					<div class=""form-check"">
						<input class=""form-check-input"" type=""checkbox"" id=""merge_checklists"" name=""merge_checklists"" checked>
						<label class=""form-check-label"" for=""merge_checklists"">Merge into one checklist per day</label>
						<div class=""form-text"">Groups all species observed on the same date into a single eBird checklist.</div>
					</div>
				</div>
				<button type=""submit"" class=""btn btn-primary px-4"">Sync</button>
			</form>
		</div>
	</div>

	<div id=""jobContainer"" style=""display:none"">
		<div class=""d-flex justify-content-between align-items-center mb-2"">
			<h5 id=""jobTitle"" class=""mb-0"">Processing...</h5>
			<a id=""downloadLink"" href=""#"" class=""btn btn-success btn-sm"" style=""display:none"">&#8595; Download CSV</a>
		</div>
		<div class=""progress mb-1"" style=""height:6px"">
			<div id=""bar"" class=""progress-bar"" style=""width:0%;transition:width 0.3s""></div>
		</div>
		<p class=""text-muted small mb-3""><span id=""message"">Starting...</span></p>
		<div id=""importHint"" class=""alert alert-info py-2 small mb-3"" style=""display:none"">
			Download the CSV, then upload it at <a href=""https://ebird.org/import/upload.form"" target=""_blank"" rel=""noopener"">ebird.org/import</a> &mdash; choose <strong>eBird Record Format (Extended)</strong>.
		</div>

		<table class=""table table-sm table-hover table-bordered"" id=""table"">
			<thead class=""table-light"">
				<tr>
					<th onclick=""sortTable(0)"">English</th>
					<th onclick=""sortTable(1)"">Latin</th>
					<th onclick=""sortTable(2)"">Date</th>
					<th onclick=""sortTable(3)"">In iNat</th>
					<th onclick=""sortTable(4)"">In eBird</th>
				</tr>
			</thead>
			<tbody></tbody>
		</table>
	</div>

</div>
<script>
	let sortCol = -1, sortDir = 1;

	document.getElementById('uploadForm').addEventListener('submit', async (e) => {
		e.preventDefault();
		sortCol = -1; sortDir = 1;
		document.getElementById('jobTitle').innerText = 'Processing...';
		document.getElementById('downloadLink').style.display = 'none';
		document.getElementById('importHint').style.display = 'none';
		const form = new FormData(document.getElementById('uploadForm'));
		const res = await fetch('/start', { method: 'POST', body: form });
		const data = await res.json();
		window.jobId = data.jobId;
		document.getElementById('jobContainer').style.display = 'block';
		poll();
	});

	function poll() {
		fetch('/status/' + window.jobId)
			.then(r => r.json())
			.then(data => {
				const pct = data.percent || 0;
				document.getElementById('bar').style.width = pct + '%';
				document.getElementById('message').innerText = data.message || '';
				const tbody = document.querySelector('#table tbody');
				tbody.innerHTML = '';
				(data.rows || []).forEach(r => {
					const tr = document.createElement('tr');
					const yesNo = v => `<span class=""badge ${v ? 'badge-yes' : 'badge-no'}"">${v}</span>`;
					const ebirdCell = r.taxonValid === false
						? `<span class=""badge badge-invalid"" title=""Scientific name not matched in eBird taxonomy — included in CSV with iNaturalist name"">unmatched</span>`
						: yesNo(r.ebird);
					tr.innerHTML = `<td>${r.english}</td><td><em>${r.latin}</em></td><td>${r.date}</td><td>${yesNo(r.inat)}</td><td>${ebirdCell}</td>`;
					tbody.appendChild(tr);
				});
				applySort();
				if (data.output) {
					const link = document.getElementById('downloadLink');
					link.href = '/download/' + data.output;
					link.style.display = 'inline-block';
				}
				if (pct >= 100) {
					document.getElementById('jobTitle').innerText = 'Done';
					document.getElementById('importHint').style.display = 'block';
				} else {
					setTimeout(poll, 1000);
				}
			})
			.catch(e => { console.error(e); setTimeout(poll, 2000); });
	}

	function sortTable(col) {
		if (sortCol === col) sortDir *= -1;
		else { sortCol = col; sortDir = 1; }
		document.querySelectorAll('#table th').forEach((th, i) => {
			th.removeAttribute('data-dir');
			if (i === sortCol) th.setAttribute('data-dir', sortDir === 1 ? 'asc' : 'desc');
		});
		applySort();
	}

	function applySort() {
		if (sortCol === -1) return;
		const tbody = document.querySelector('#table tbody');
		const rows = Array.from(tbody.querySelectorAll('tr'));
		rows.sort((a, b) => {
			const av = a.children[sortCol].innerText.trim();
			const bv = b.children[sortCol].innerText.trim();
			let cmp;
			if (sortCol === 2) {
				cmp = new Date(av) - new Date(bv);
			} else if (sortCol === 3 || sortCol === 4) {
				cmp = (av === 'true' ? 1 : 0) - (bv === 'true' ? 1 : 0);
			} else {
				cmp = av.localeCompare(bv);
			}
			return cmp * sortDir;
		});
		rows.forEach(r => tbody.appendChild(r));
	}
</script>
</body>
</html>
	";
	return Results.Content(html, "text/html; charset=utf-8");
});

app.MapGet("/favicon.svg", () =>
{
	var svg = @"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""0 0 32 32"">
  <rect width=""32"" height=""32"" rx=""6"" fill=""#74ac00""/>
  <!-- sync arc: top half, left to right -->
  <path d=""M6 19 A11 11 0 1 1 26 19"" stroke=""white"" stroke-width=""2.5"" fill=""none"" stroke-linecap=""round""/>
  <!-- arrowhead at end of top arc (lower-right), pointing down-right -->
  <polygon points=""23,23 27,17 30,21"" fill=""white""/>
  <!-- sync arc: bottom half, right to left -->
  <path d=""M26 13 A11 11 0 1 1 6 13"" stroke=""white"" stroke-width=""2.5"" fill=""none"" stroke-linecap=""round""/>
  <!-- arrowhead at end of bottom arc (upper-left), pointing up-left -->
  <polygon points=""9,9 5,15 2,11"" fill=""white""/>
  <!-- bird M-wing silhouette -->
  <path d=""M10 17 Q13 12 16 15 Q19 12 22 17"" stroke=""white"" stroke-width=""2"" fill=""none"" stroke-linecap=""round"" stroke-linejoin=""round""/>
</svg>";
	return Results.Content(svg, "image/svg+xml");
});

app.MapPost("/start", async (HttpRequest request, aviansync.Services.InatService inat, aviansync.Services.EbirdTaxonomyService taxonomy) =>
{
	var form = await request.ReadFormAsync();
	var userId = form["user_id"].ToString();
	var file = request.Form.Files[0];
	var lifePath = Path.Combine(uploadsDir, Path.GetFileName(file.FileName));
	using (var stream = File.Create(lifePath)) await file.CopyToAsync(stream);

	var jobId = Guid.NewGuid().ToString("N");
	WriteProgress(jobId, new { percent = 0, message = "Queued", rows = new List<object>() });

	// Start background job
	_ = Task.Run(async () => await ProcessJobAsync(jobId, userId, lifePath, form, inat, taxonomy));

	return Results.Json(new { jobId });
});

async Task ProcessJobAsync(string jobId, string userId, string lifePath, IFormCollection form, aviansync.Services.InatService inat, aviansync.Services.EbirdTaxonomyService taxonomy)
{
	try
	{
		WriteProgress(jobId, new { percent = 0, message = "Starting", rows = new List<object>() });

		// Fetch observations
		var observations = await inat.FetchAllObservations(userId);
		var total = observations.Count;
		WriteProgress(jobId, new { percent = 5, message = $"Fetched {total} observations", rows = new List<object>() });

		// Load life list then immediately discard the uploaded file
		var lifeSet = aviansync.Services.EbLifeListService.LoadScientificNames(lifePath);
		var lifeListCount = lifeSet.Count;
		try { File.Delete(lifePath); } catch { }

		// HTML checkboxes only POST when checked — absence means unchecked
		var displayAll       = form.ContainsKey("display_all");
		var mergeChecklists  = form.ContainsKey("merge_checklists");
		var ebirdApiKey      = form["ebird_api_key"].ToString();

		// Fetch eBird taxonomy for validation (null = no key provided or fetch failed)
		Dictionary<string, string>? ebirdTaxa = null;
		string taxonomyStatus;
		if (string.IsNullOrWhiteSpace(ebirdApiKey))
		{
			taxonomyStatus = $"Loaded life list with {lifeListCount} species (no eBird API key — taxonomy validation skipped)";
		}
		else
		{
			try
			{
				ebirdTaxa = await taxonomy.GetTaxonomyAsync(ebirdApiKey);
				taxonomyStatus = $"Loaded life list ({lifeListCount} species) · eBird taxonomy ({ebirdTaxa!.Count} species)";
			}
			catch (Exception ex)
			{
				taxonomyStatus = $"Loaded life list with {lifeListCount} species (eBird taxonomy fetch failed: {ex.Message})";
			}
		}
		WriteProgress(jobId, new { percent = 10, message = taxonomyStatus, rows = new List<object>() });

		var entries = new List<aviansync.Models.EbirdEntry>();
		var rows    = new List<aviansync.Models.RowData>();


		for (int i = 0; i < observations.Count; i++)
		{
			var obs = observations[i];
			try
			{
				var id = obs.GetProperty("id").GetInt32();
				string timeObserved = obs.TryGetProperty("time_observed_at", out var t) && t.ValueKind != System.Text.Json.JsonValueKind.Null
					? t.GetString() ?? "" : "";
				DateTime dt;
				if (!string.IsNullOrWhiteSpace(timeObserved))
				{
					if (timeObserved.EndsWith("Z") || timeObserved.Contains("+"))
						dt = DateTime.Parse(timeObserved, null, System.Globalization.DateTimeStyles.AdjustToUniversal).ToLocalTime();
					else
						dt = DateTime.Parse(timeObserved);
				}
				else
				{
					// Fall back to observed_on (date-only field, always present)
					var observedOn = obs.TryGetProperty("observed_on", out var od) && od.ValueKind != System.Text.Json.JsonValueKind.Null
						? od.GetString() ?? "" : "";
					if (string.IsNullOrWhiteSpace(observedOn)) continue;
					dt = DateTime.Parse(observedOn);
				}

				var date = dt.ToString("MM/dd/yyyy");
				var startTime = dt.ToString("HH:mm");
				var place = obs.TryGetProperty("place_guess", out var pg) ? pg.GetString() ?? "" : "";
				var loc = obs.TryGetProperty("location", out var l) ? l.GetString() ?? "" : "";
				string lat = "", lon = "";
				if (!string.IsNullOrWhiteSpace(loc) && loc.Contains(","))
				{
					var parts = loc.Split(',');
					lat = parts[0].Trim();
					lon = parts[1].Trim();
				}
			string taxonName = "";
			string commonName = "";
			if (obs.TryGetProperty("taxon", out var taxon) && taxon.ValueKind == System.Text.Json.JsonValueKind.Object)
			{
				if (taxon.TryGetProperty("name", out var tn)) taxonName = tn.GetString() ?? "";
				if (taxon.TryGetProperty("preferred_common_name", out var pcn))
					commonName = pcn.GetString() ?? "";
				if (string.IsNullOrWhiteSpace(commonName) && taxon.TryGetProperty("english_name", out var en))
					commonName = en.GetString() ?? "";
			}
			var genus = "";
			var speciesEpithet = "";
			var binomial = "";
			if (!string.IsNullOrWhiteSpace(taxonName))
			{
				var toks = taxonName.Split(' ');
				if (toks.Length >= 1) genus = toks[0];
				if (toks.Length >= 2) { speciesEpithet = toks[1]; binomial = $"{toks[0]} {toks[1]}"; }
			}

			// eBird taxonomy validation: match on binomial to handle subspecies/domestic forms from iNat
			var ebirdComName = ebirdTaxa != null && !string.IsNullOrEmpty(binomial) && ebirdTaxa.TryGetValue(binomial, out var cn) ? cn : null;
			var taxonValid = ebirdTaxa == null || ebirdComName != null;

			// Life list check uses binomial so subspecies (e.g. "Anas platyrhynchos domesticus") matches "Anas platyrhynchos"
			var presentInEbird = !string.IsNullOrWhiteSpace(binomial) && lifeSet.Contains(binomial);

			// Add row for display (regardless of filtering)
			rows.Add(new aviansync.Models.RowData
			{
				English    = commonName,
				Latin      = taxonName,
				Date       = date,
				Inat       = true,
				Ebird      = presentInEbird,
				TaxonValid = taxonValid
			});

			// Filter entries based on displayAll checkbox
			if (!displayAll && presentInEbird) continue;
			
			var entry = new aviansync.Models.EbirdEntry
			{
				CommonName = ebirdComName ?? commonName, // prefer eBird's name so importer can match it
				Genus = genus,
				Species = speciesEpithet,
				Number = "X",
				SpeciesComments = "",
				Location = place,
				Latitude = lat,
				Longitude = lon,
				Date = date,
				StartTime = startTime,
				SubmissionComments = $"iNaturalist observation https://www.inaturalist.org/observations/{id}",
			};

				entries.Add(entry);
			}
			catch { /* ignore single-observation errors */ }

			// Update progress
			int percent = 5 + (int)((i + 1) / (double)total * 90);
			WriteProgress(jobId, new { percent, message = $"Processing {i + 1}/{total}", rows });
		}

		// Merge: one checklist per day — deduplicate species, share first observation's location/time
		if (mergeChecklists)
		{
			entries = entries
				.GroupBy(e => e.Date)
				.SelectMany(g =>
				{
					var anchor = g.First();
					return g
						.GroupBy(e => (e.Genus, e.Species))
						.Select(sg =>
						{
							var e = sg.First();
							e.Location   = anchor.Location;
							e.Latitude   = anchor.Latitude;
							e.Longitude  = anchor.Longitude;
							e.StartTime  = anchor.StartTime;
							e.StateProvince = anchor.StateProvince;
							e.CountryCode   = anchor.CountryCode;
							e.SubmissionComments = string.Join(", ", sg.Select(x => x.SubmissionComments).Where(s => !string.IsNullOrEmpty(s)));
							return e;
						});
				})
				.ToList();
		}

		// Write CSV
		var outFile = aviansync.Services.CsvService.WriteEntries(entries, uploadsDir, userId);
		WriteProgress(jobId, new { percent = 100, message = "Done", rows, output = Path.GetFileName(outFile) });
		jobs[jobId] = outFile;
	}
	catch (Exception ex)
	{
		WriteProgress(jobId, new { percent = 100, message = $"Error: {ex.Message}", rows = new List<object>() });
	}
}

app.MapGet("/status/{jobId}", (string jobId) =>
{
	var progress = ReadProgress(jobId);
	if (progress != null) return Results.Json(progress);
	return Results.Json(new { percent = 0, message = "Not found", rows = new List<object>() });
});

app.MapGet("/download/{filename}", (string filename) =>
{
	var path = Path.Combine(uploadsDir, filename);
	if (!File.Exists(path)) return Results.NotFound();
	var bytes = File.ReadAllBytes(path);
	// Clean up output file and its associated progress file
	try { File.Delete(path); } catch { }
	var jobId = jobs.FirstOrDefault(j => Path.GetFileName(j.Value) == filename).Key;
	if (jobId != null)
	{
		jobs.TryRemove(jobId, out _);
		progress.TryRemove(jobId, out _);
	}
	return Results.File(bytes, "text/csv", filename);
});

app.Run();
