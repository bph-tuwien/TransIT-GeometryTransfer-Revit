using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System.Collections.Generic;

namespace TransITGeometryTransferRevit
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class Main : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Document document = commandData.Application.ActiveUIDocument.Document;// replace var with Document (statically-typed)

            //ElementId level_id = new ElementId(0); // replace var with ElementId, I assume "1526" is the correct level id in your local project
            ElementId level_id = ViewLevel(commandData);
            ElementId wallTypeId = document.GetDefaultElementTypeId(ElementTypeGroup.WallType);// replace var with Element Id (statically-typed)
            //
            // create line on which you will construct a new wall
            //
            XYZ point_a = new XYZ(-100, 0, 0);
            XYZ point_b = new XYZ(100, 0, 0); // for start try making a wall in one plane
            Curve line = Line.CreateBound(point_a, point_b) as Curve; // Create a bound curve for function to work, a wall cannot be created with unbound line

            Transaction transaction = new Transaction(document);// no need to to use "Using", Autodesk disposes of all the methods and data in Execute method.
            {
                transaction.Start("create walls");

                // this command below creates a wall, with default wall type
                Wall wall = Wall.Create(document, line, level_id, false);

                // no need of this code, I have commented out this code ( you may remove it). Also the newfamilyinstance method is not used to create a wall.

                /*var position = new XYZ(0, 0, 0);
                var symbolId = document.GetDefaultFamilyTypeId(new ElementId(BuiltInCategory.OST_Walls));
                if (symbolId == ElementId.InvalidElementId)
                {
                    transaction.RollBack();
                    return Result.Failed;
                }
                var symbol = document.GetElement(symbolId) as FamilySymbol;
                var level = (Level)document.GetElement(wall.LevelId);
                document.Create.NewFamilyInstance(position, symbol, wall, level, StructuralType.NonStructural);
                */
                transaction.Commit();
            }
            return Result.Succeeded;



        }


        public ElementId ViewLevel(ExternalCommandData commandData)
        {
            Document doc = commandData.Application.ActiveUIDocument.Document;
            View active = doc.ActiveView;
            ElementId levelId = null;

            Parameter level = active.LookupParameter("Associated Level");

            FilteredElementCollector lvlCollector = new FilteredElementCollector(doc);
            ICollection<Element> lvlCollection = lvlCollector.OfClass(typeof(Level)).ToElements();

            foreach (Element l in lvlCollection)
            {
                Level lvl = l as Level;
                if (lvl.Name == level.AsString())
                {
                    levelId = lvl.Id;
                    //TaskDialog.Show("test", lvl.Name + "\n"  + lvl.Id.ToString());
                }
            }

            return levelId;

        }

    }
}
