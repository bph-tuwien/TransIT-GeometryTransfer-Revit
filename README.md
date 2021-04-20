# TransIT Geometry Transfer Revit

## Usage

The following steps are required to use the plugin in Revit:
 1. Build the solution to get the plugin artifact, either Debug or Release.
 2. Edit the `TransITGeometryTransferRevit.addin` file's (found in this repository's root) `Assembly` tag to point to
    the plugin's dll built in the previous step.
 3. Copy this addin file to the following directory: `C:\ProgramData\Autodesk\Revit\Addins\2021` or similar depending on
    the given Revit version.
 4. In Revit the plugin's command will appear under `Add-ins/External Tools`.

