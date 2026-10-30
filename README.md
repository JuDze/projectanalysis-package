# Project Analysis

Unity Editor package for reviewing textures, models and audio using 13 configurable rules. Requires Unity 6000.3.10f1 or a compatible Unity 6.3 editor.

## Installation

Open **Window > Package Management > Package Manager**, select **+ > Install package from git URL**, and enter:

```text
https://github.com/JuDze/projectanalysis-package.git
```

Git must be available to Unity for installation from a Git URL. Alternatively, download or clone this repository, choose **Install package from disk**, and select `package.json`.

## Usage

Open **Tools > Project Analysis**. Choose all project assets or a folder, configure the rules and run analysis. Review findings and proposed changes before confirming fixes. **Undo** and **Revert all fixes** restore changes from the current analysis session.

**Create report** captures an analysis snapshot. **Save Report** exports the selected report as JSON. Optional **Send to Web** uploads that report to a compatible reporting server using the server address, project UUID and access token entered in **Web Connection**. **Test connection** checks access without uploading a report. Local analysis and JSON export do not require a web connection.

Settings and current-session fix state are stored in the Unity project's `UserSettings/ProjectAnalysis` directory. Access tokens are stored only for the current Unity Editor session.

See [the documentation](Documentation/Project%20Analysis.md) for the rules, workflow and limitations.

This repository distributes the installable package. Development sources and tests are maintained separately in [project-analysis](https://github.com/JuDze/project-analysis). The distributed package does not include tests or depend on Unity Test Framework.
