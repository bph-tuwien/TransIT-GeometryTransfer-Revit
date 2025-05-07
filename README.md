# TransIT Geometry Transfer Revit

## Usage

The following steps are required to use the plugin in Revit:
 1. Build the solution to get the plugin artifact, either Debug or Release.
 2. Edit the `TransITGeometryTransferRevit.addin` file's (found in this repository's root) `Assembly` tag to point to
    the plugin's dll built in the previous step.
 3. Copy this addin file to the following directory: `C:\ProgramData\Autodesk\Revit\Addins\2021` or similar depending on
    the given Revit version.
 4. Open Revit and create a new `English/Conceptual Mass/Metric Mass` family. The plugin is written in a way that it can
    only be executed in a family document and not in a model document.
 5. The plugin can be executed under `Add-ins/External Tools/TransIT Geometry Transfer Revit`.
 6. When executed, the plugin asks for the exported tunnel IFC file. Test files can be found under the given release.

## Known Issues

 1. The exported IFC file from Revit gives IfcShapeRepresentation.CorrectItemsForType schema violation in FZKViewer for
 each tunnel section because of their Body representations. Currently the representation uses `AdvancedSweptSolid` which
 should be the correct one based on the IFC documentation, however FZKViewer doesn't recognizes it. Other representation
 types were tried for the `IfcSectionedSolidHorizontal` representation item, but those resulted in incorrect shape
 representations in FZKViewer. 
 See RepresentationType in the IfcShapeRepresentation's [documentation](https://standards.buildingsmart.org/IFC/RELEASE/IFC4_3/HTML/lexical/IfcShapeRepresentation.htm).

## Documentation and Tutorials

 * [Tunnel Tutorial](Docs/tunnel-tutorial.md)
 * [Family System](Docs/family-system.md)

