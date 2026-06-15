# Changelog
All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](http://keepachangelog.com/en/1.0.0/)
and this project adheres to [Semantic Versioning](http://semver.org/spec/v2.0.0.html).

## [0.6.0] - 2026-06-15

### Added

- Added optional local MediaPipe pose tracking setup with an embedded stripped Homuler package, installer, validator, quickstart scene helper, and generated `LocalLandmarkProvider` integration.
- Extended `SkeletonProviderSwitcher` with a tertiary provider slot for external live, local model, and playback workflows.

## [0.5.0] - 2026-06-10

### Added

- Added pose stream recording and playback components with JSONL and binary recording formats.

## [0.4.0] - 2026-06-08

### Added

- Proper Unity Rig support

### Changed

- Reorganized runtime scripts around the skeleton-provider architecture.
- Expanded student-facing documentation for provider blocks, pipeline order, and extension points.
- Simplified smoothing setup by keeping algorithm classes testable without a public factory type.

### Removed

- Removed the unused `AccumulatedBuffer` type.

## [0.1.0] - 2026-05-20

### Added

- Initial HEX Tracking Connector package release.
