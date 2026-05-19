# aviansync

Migrate your [iNaturalist](https://www.inaturalist.org) bird observations to [eBird](https://ebird.org).

Fetches all research-grade bird observations for an iNaturalist user, compares them against your existing eBird life list, and produces a CSV in the [eBird record format](https://support.ebird.org/en/support/solutions/articles/48000907878) ready to upload.

## Quick start

**Requirements:** Docker

```bash
docker-compose up --build
```

Open [http://localhost:8080](http://localhost:8080).

## Usage

1. **Get your eBird data export**
   Sign in at [ebird.org/downloadMyData](https://ebird.org/downloadMyData) and download `MyEBirdData.csv`.

2. **Fill in the form**
   - Enter your iNaturalist username
   - Upload `MyEBirdData.csv`
   - Choose options:
     - **Display all entries** — when unchecked, only species missing from your eBird list are included in the exported CSV
     - **Merge into one checklist per day** — groups all species observed on the same date into a single eBird checklist instead of one incidental checklist per observation

3. **Download the CSV**
   When processing finishes click *Download CSV* and import it at [ebird.org/import](https://ebird.org/import/upload.form?theme=ebird).

## File lifecycle

Uploaded files are deleted from the server as soon as they are no longer needed:
- The eBird life list is deleted immediately after it is read into memory.
- The generated CSV is deleted from the server as soon as you download it.
- Any leftover files from a previous run are cleared on startup.

## Credits

Inspired by [inat2ebird](https://github.com/lubianat/inat2ebird) by [Tiago Lubiana](https://github.com/lubianat).

## Project structure

```
aviansync/
├── aviansync/               # ASP.NET minimal API
│   ├── Models/
│   │   ├── EbirdEntry.cs        # eBird record format model
│   │   └── RowData.cs           # UI table row model
│   ├── Services/
│   │   ├── InatService.cs       # iNaturalist API pagination
│   │   ├── EbLifeListService.cs # eBird life list CSV parser
│   │   └── CsvService.cs        # eBird CSV writer
│   ├── Program.cs               # Web app, routes, processing logic
│   ├── aviansync.csproj
│   └── Dockerfile
├── docker-compose.yml
└── README.md
```
