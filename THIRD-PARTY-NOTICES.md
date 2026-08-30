# Third-Party Notices

## Visual Studio Code Codicons

NGR Launcher includes adapted icon path geometry from the Microsoft Visual Studio Code Codicons project:

- Project: `microsoft/vscode-codicons`
- Source: https://github.com/microsoft/vscode-codicons
- License: Creative Commons Attribution 4.0 International (CC BY 4.0)
- License text: https://creativecommons.org/licenses/by/4.0/
- Copyright / attribution: Microsoft and the Visual Studio Code Codicons contributors

The following Codicons are currently used: `home`, `tools`, `library`, `settings-gear`, `play`, `save`, `add`, `trash`, `chevron-up`, and `chevron-down`.

For NGR Launcher, the original SVG path data was converted to WPF `Geometry` resources so the icons can inherit the application's theme colors without adding another runtime icon package. The shapes are otherwise used as provided by the Codicons project.
