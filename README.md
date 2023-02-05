# One Pace Jellyfin Integration

This [Jellyfin](https://jellyfin.org/) plugin provides metadata and cover art integration for the [One Pace](https://onepace.net/) project.

### Series Overview
![Series Overview](docs/series.png)

### Arc Overview
![Arc Overview](docs/arc.png)

## Installation

1. Configure the [@jwueller Jellyfin Plugin Repository](https://github.com/jwueller/jellyfin-repository)
2. Install the "One Pace" plugin from the catalog

### Configure the Library

Make sure you have a library of content type "Shows". To create a new one:

1. Go to: Dashboard - Libraries - Add Media Library
2. Set "Content Type" to "Shows"

Enable the "One Pace" downloaders you want to use (most likely all of them) and move them to the top if they aren't already to ensure they take priority:

* Metadata downloaders (TV Shows)
* Metadata downloaders (Seasons)
* Metadata downloaders (Episodes)
* Image fetchers (TV Shows)
* Image fetchers (Seasons)
* Image fetchers (Episodes)

If the library was previously scanned, you might have to manually identify or refresh series metadata.

## Recommended File Structure

This integration is designed to work directly with the released files from the One Pace project.

* Paths containing "One Pace" will be scanned.
* Arcs are matched by number or title. You can use empty folders as placeholders for unreleased arcs.
* Episodes are matched by manga chapter range or title.

### Example

```
/media/anime/One Pace/
├── Arc 01 - Romance Dawn
│   ├── [One Pace][1] Romance Dawn 01 [1080p][D767799C].mkv
│   ├── [One Pace][2] Romance Dawn 02 [1080p][04A43CEF].mkv
│   ├── [One Pace][3-5] Romance Dawn 03 [1080p][C7CA5080].mkv
│   └── [One Pace][5-7] Romance Dawn 04 [1080p][09DD81D3].mkv
├── Arc 02 - Orange Town
│   ├── [One Pace][11-16] Orange Town 02 [480p][3D7957D8].mkv
│   ├── [One Pace][17-21] Orange Town 03 [480p][800263CF].mkv
│   └── [One Pace][8-11] Orange Town 01 [480p][A2F5F372].mkv
├── Arc 03 - Syrup Village
│   ├── [One Pace][23-25] Syrup Village 01 [480p][B19F374A].mkv
│   ├── [One Pace][26-27] Syrup Village 02 [480p][7EE6C65F].mkv
│   ├── [One Pace][28-30] Syrup Village 03 [480p][C2C0A86A].mkv
│   ├── [One Pace][31-34] Syrup Village 04 [480p][FD399699].mkv
│   ├── [One Pace][35-39] Syrup Village 05 [480p][5498C538].mkv
│   └── [One Pace][40-41] Syrup Village 06 [480p][D1742A98].mkv
├── ...
```
