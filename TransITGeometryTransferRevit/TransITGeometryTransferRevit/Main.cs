using System;
using System.Collections.Generic;
using System.IO;

using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using CreationApplication = Autodesk.Revit.Creation.Application;
using FamilyItemFactory = Autodesk.Revit.Creation.FamilyItemFactory;
using Autodesk.Revit.ApplicationServices;

using Xbim.Ifc;
using Xbim.Ifc4.GeometryResource;
using Xbim.Ifc4.Kernel;
using Xbim.Ifc4.RepresentationResource;

using TransITGeometryTransferRevit.Ifc.GeometryResource;
using System.Linq;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI.Selection;

namespace TransITGeometryTransferRevit
{
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class Main : IExternalCommand
    {









        /// <summary>
        /// Family template filename extension
        /// </summary>
        const string _family_template_ext = ".rft";

        /// <summary>
        /// Revit family filename extension
        /// </summary>
        const string _rfa_ext = ".rfa";

        /// <summary>
        /// Family template library path
        /// </summary>
        const string _path = "C:/ProgramData/Autodesk/RVT 2021/Family Templates/English";

        /// <summary>
        /// Family template filename stem
        /// </summary>
        const string _family_template_name = "Metric Generic Model";

        // family template path and filename for imperial units

        //const string _path = "C:/ProgramData/Autodesk/RST 2012/Family Templates/English_I";
        //const string _family_name = "Structural Stiffener";

        /// <summary>
        /// Name of the generated stiffener family
        /// </summary>
        const string _family_name = "Tunnel Profile";

        /// <summary>
        /// Conversion factor from millimetre to foot
        /// </summary>
        const double _mm_to_foot = 1 / 304.8;

        /// <summary>
        /// Convert a given length to feet
        /// </summary>
        double MmToFoot(double length_in_mm)
        {
            return _mm_to_foot * length_in_mm;
        }

        /// <summary>
        /// Convert a given point defined in millimetre units to feet
        /// </summary>
        XYZ MmToFootPoint(XYZ p)
        {
            return p.Multiply(_mm_to_foot);
        }

        static int n = 4;

        /// <summary>
        /// Extrusion profile points defined in millimetres.
        /// Here is just a very trivial rectangular shape.
        /// </summary>
        static List<XYZ> _countour = new List<XYZ>(n)
    {
      new XYZ( 0 , -75 , 0 ),
      new XYZ( 508, -75 , 0 ),
      new XYZ( 508, 75 , 0 ),
      new XYZ( 0, 75 , 0 )
    };

        /// <summary>
        /// Extrusion thickness for stiffener plate
        /// </summary>
        const double _thicknessMm = 20.0;

        /// <summary>
        /// Return the first element found of the 
        /// specific target type with the given name.
        /// </summary>
        Element FindElement(
          Document doc,
          Type targetType,
          string targetName)
        {
            return new FilteredElementCollector(doc)
              .OfClass(targetType)
              .First<Element>(e => e.Name.Equals(targetName));

            // parse the collection for the given name
            // using LINQ query here. 

            //var targetElems 
            //  = from element in collector 
            //    where element.Name.Equals( targetName ) 
            //    select element;

            //return targetElems.First<El
            //List<Element> elems = targetElems.ToList<Element>();

            //if( elems.Count > 0 )
            //{  // we should have only one with the given name. 
            //  return elems[0];
            //}

            // cannot find it.
            //return null;

            /*
            // most efficient way to find a named 
            // family symbol: use a parameter filter.

            ParameterValueProvider provider
              = new ParameterValueProvider(
                new ElementId( BuiltInParameter.DATUM_TEXT ) ); // VIEW_NAME for a view

            FilterStringRuleEvaluator evaluator
              = new FilterStringEquals();

            FilterRule rule = new FilterStringRule(
              provider, evaluator, targetName, true );

            ElementParameterFilter filter
              = new ElementParameterFilter( rule );

            return new FilteredElementCollector( doc )
              .OfClass( targetType )
              .WherePasses( filter )
              .FirstElement();
            */
        }

        /// <summary>
        /// Convert a given list of XYZ points 
        /// to a CurveArray instance. 
        /// The points are defined in millimetres, 
        /// the returned CurveArray in feet.
        /// </summary>
        CurveArray CreateProfile(
          List<XYZ> pts,
          CreationApplication creapp)
        {
            CurveArray profile = new CurveArray();

            int n = _countour.Count;

            for (int i = 0; i < n; ++i)
            {
                int j = (0 == i) ? n - 1 : i - 1;

                profile.Append(Line.CreateBound(
                  MmToFootPoint(pts[j]),
                  MmToFootPoint(pts[i])));
            }
            return profile;
        }

        /// <summary>
        /// Create an extrusion from a given thickness 
        /// and list of XYZ points defined in millimetres
        /// in the given family document, which  must 
        /// contain a sketch plane named "Ref. Level".
        /// </summary>
        Extrusion CreateExtrusion(
          Document doc,
          List<XYZ> pts,
          double thickness)
        {
            FamilyItemFactory factory = doc.FamilyCreate;

            CreationApplication creapp = doc.Application.Create;

            //SketchPlane sketch = doc.get_Element( 
            //  new ElementId( 501 ) ) as SketchPlane;

            SketchPlane sketch = FindElement(doc,
              typeof(SketchPlane), "Ref. Level")
                as SketchPlane;

            CurveArrArray curveArrArray = new CurveArrArray();

            curveArrArray.Append(CreateProfile(pts, creapp));

            double extrusionHeight = MmToFoot(thickness);

            return factory.NewExtrusion(true,
              curveArrArray, sketch, extrusionHeight);
        }




        private Family LoadFamilyIfNotLoaded(Document doc, string filename)
        {
            // before loading the family, it needs to be checked wheter it is already loaded or not

            Family family = null;

            FilteredElementCollector a = new FilteredElementCollector(doc).OfClass(typeof(Family));

            int n = a.Count<Element>(e => e.Name.Equals(_family_name));

            if (0 < n)
            {
                family = a.First<Element>(e => e.Name.Equals(_family_name)) as Family;
            }
            else
            {
                doc.LoadFamily(filename, out family);
            }

            return family;
        }

        private FamilySymbol GetFirstFamilySymbol(Family family)
        {
            FamilySymbol symbol = null;

            ISet<ElementId> familySymbolIds = family.GetFamilySymbolIds();

            // Get family symbols which is contained in this family
            foreach (ElementId id in familySymbolIds)
            {
                FamilySymbol familySymbol = family.Document.GetElement(id) as FamilySymbol;

                symbol = familySymbol;

                // TODO: Debug this context
                break;
            }

            return symbol;
        }




        /// <summary>
        /// Testing family scripting and creation
        /// </summary>
        /// <param name="commandData"></param>
        /// <param name="message"></param>
        /// <param name="elements"></param>
        /// <returns></returns>
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {


            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Application app = uiapp.Application;
            Document doc = uidoc.Document;

            if (null == doc)
            {
                message = "Please run this command in an open document.";
                return Result.Failed;
            }


            #region Create a new structural stiffener family

            string templateFileName = Path.Combine(_path, _family_template_name + _family_template_ext);

            Document fdoc = app.NewFamilyDocument(templateFileName);

            if (null == fdoc)
            {
                message = "Cannot create family document.";
                return Result.Failed;
            }


            Transaction revitTransaction = new Transaction(fdoc, "Creating tunnel profile family");
            {
                revitTransaction.Start();

                CreateExtrusion(fdoc, _countour, _thicknessMm);

                revitTransaction.Commit();
            }

            // save our new family background document
            // and reopen it in the Revit user interface:

            string filename = Path.Combine(Path.GetTempPath(), _family_name + _rfa_ext);

            SaveAsOptions opt = new SaveAsOptions();
            opt.OverwriteExistingFile = true;

            fdoc.SaveAs(filename, opt);

            fdoc.Close(false);

            #endregion // Create a new structural stiffener family







            #region Insert stiffener family instance

            revitTransaction = new Transaction(doc, "Inserting tunnel profile family instance");
            {
                revitTransaction.Start();


                Family family = LoadFamilyIfNotLoaded(doc, filename);
                FamilySymbol symbol = GetFirstFamilySymbol(family);

                // Make sure to activate symbol
                if (!symbol.IsActive)
                { symbol.Activate(); doc.Regenerate(); }


                XYZ p = new XYZ(0, 0, 0);
                StructuralType st = StructuralType.UnknownFraming;

                doc.Create.NewFamilyInstance(p, symbol, st);


                revitTransaction.Commit();
            }



            


            #endregion // Insert stiffener family instance

            return Result.Succeeded;

        }

















        /// <summary>
        /// The entry point of the plugin. 
        /// </summary>
        /// <param name="commandData"></param>
        /// <param name="message"></param>
        /// <param name="elements"></param>
        /// <returns></returns>
        public Result ExecuteGeometryImportRaw(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            Document document = commandData.Application.ActiveUIDocument.Document;


            Transaction revitTransaction = new Transaction(document);
            {
                revitTransaction.Start("Importing 3d tunnel");


                var ifcFilePath = UserInteractions.PromptIfcFileOpenDialog();

                FileInfo ifcFileInfo = new FileInfo(ifcFilePath);

                using (var model = IfcStore.Open(ifcFileInfo.FullName))
                {
                    using (var ifcTransaction = model.BeginTransaction("Reading 3d tunnel to recreate in Revit"))
                    {

                        var objects = model.Instances.OfType<IfcProduct>();

                        foreach (var obj in objects)
                        {

                            // TODO: Refactor this, should be a better way of finding the tunnel
                            if (obj.Representation == null || obj.Representation.Representations.Count != 3)
                            {
                                continue;
                            }

                            var representations = obj.Representation.Representations;


                            IfcRepresentation axisRepresentation = null;
                            IfcRepresentation profileRepresentation = null;

                            foreach (var representation in representations)
                            {
                                if (representation.RepresentationIdentifier == "Axis")
                                {
                                    axisRepresentation = representation;
                                }

                                if (representation.RepresentationIdentifier == "Profile")
                                {
                                    profileRepresentation = representation;
                                }
                            }

                            IfcIndexedPolyCurve ifcTunnelLine = axisRepresentation.Items[0] as IfcIndexedPolyCurve;

                            List<CurveLoop> revitProfiles = new List<CurveLoop>();

                            foreach (var ifcProfileItem in profileRepresentation.Items)
                            {
                                var ifcProfileCurve = ifcProfileItem as IfcIndexedPolyCurve;
                                var revitProfile = ifcProfileCurve.ToCurveLoop();
                                revitProfiles.Add(revitProfile);
                            }

                            var revitTunnelLine = ifcTunnelLine.ToCurve();

                            List<double> pathParams = new List<double>();

                            // TODO: FIX THIS - Profile placement is NOT implemented! Arbitrary values
                            pathParams.Add(revitTunnelLine.GetEndParameter(0));
                            pathParams.Add(revitTunnelLine.GetEndParameter(1));


                            Category directShapeCategory = document.Settings.Categories.get_Item(BuiltInCategory.OST_GenericModel);

                            DirectShape directShape = DirectShape.CreateElement(document, directShapeCategory.Id);

                            Solid solid = GeometryCreationUtilities.CreateSweptBlendGeometry(revitTunnelLine, pathParams,
                                                                                             revitProfiles, null);

                            List<GeometryObject> gs = new List<GeometryObject>();
                            gs.Add(solid);
                            directShape.AppendShape(gs);

                        }
                    }
                }

                revitTransaction.Commit();
            }

            return Result.Succeeded;
        }

        [Obsolete("Testing out how Revit loft generation works")]
        public Result ExecuteLoftTest(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            // https://thebuildingcoder.typepad.com/blog/2018/01/create-swept-blend-in-c.html

            Document document = commandData.Application.ActiveUIDocument.Document;// replace var with Document (statically-typed)

            //ElementId level_id = new ElementId(0); // replace var with ElementId, I assume "1526" is the correct level id in your local project
            //ElementId level_id = ViewLevel(commandData);
            //ElementId wallTypeId = document.GetDefaultElementTypeId(ElementTypeGroup.WallType);// replace var with Element Id (statically-typed)





            Transaction transaction = new Transaction(document);// no need to to use "Using", Autodesk disposes of all the methods and data in Execute method.
            {
                transaction.Start("create tunnel");


                Autodesk.Revit.DB.Category directShapeCategory = document.Settings.Categories.get_Item(Autodesk.Revit.DB.BuiltInCategory.OST_GenericModel);

                //if (directShapeCategory == null)
                //    return nullptr;

                Autodesk.Revit.DB.DirectShape directShape
                  = Autodesk.Revit.DB.DirectShape.CreateElement(
                    document, directShapeCategory.Id);


                // Create a path curve

                List<XYZ> controlPoints = new List<XYZ>();
                controlPoints.Add(new XYZ(0, 0, 0));
                controlPoints.Add(new XYZ(0, 0, 10));
                controlPoints.Add(new XYZ(0, 10, 10));
                controlPoints.Add(new XYZ(0, 10, 20));
                List<double> weights = new List<double>();
                weights.Add(1.0);
                weights.Add(1.0);
                weights.Add(1.0);
                weights.Add(1.0);
                Curve pathCurve = NurbSpline.CreateCurve(controlPoints, weights);


                // Create a bottom profile

                List<XYZ> bottomProfilePoints = new List<XYZ>();
                bottomProfilePoints.Add(new XYZ(5, 5, 0));
                bottomProfilePoints.Add(new XYZ(-5, 5, 0));
                bottomProfilePoints.Add(new XYZ(-5, -5, 0));
                bottomProfilePoints.Add(new XYZ(5, -5, 0));

                CurveLoop bottomProfile = new CurveLoop();

                bottomProfile.Append(Line.CreateBound(
                  bottomProfilePoints[0], bottomProfilePoints[1]));
                bottomProfile.Append(Line.CreateBound(
                  bottomProfilePoints[1], bottomProfilePoints[2]));
                bottomProfile.Append(Line.CreateBound(
                  bottomProfilePoints[2], bottomProfilePoints[3]));
                bottomProfile.Append(Line.CreateBound(
                  bottomProfilePoints[3], bottomProfilePoints[0]));


                // Create a top profile

                List<XYZ> topProfilePoints = new List<XYZ>();
                topProfilePoints.Add(new XYZ(2, 10 + 2, 20));
                topProfilePoints.Add(new XYZ(-2, 10 + 2, 20));
                topProfilePoints.Add(new XYZ(-2, 10 - 2, 20));
                topProfilePoints.Add(new XYZ(2, 10 - 2, 20));

                CurveLoop topProfile = new CurveLoop();

                topProfile.Append(Line.CreateBound(
                  topProfilePoints[0], topProfilePoints[1]));
                topProfile.Append(Line.CreateBound(
                  topProfilePoints[1], topProfilePoints[2]));
                topProfile.Append(Line.CreateBound(
                  topProfilePoints[2], topProfilePoints[3]));
                topProfile.Append(Line.CreateBound(
                  topProfilePoints[3], topProfilePoints[0]));

                List<CurveLoop> profiles = new List<CurveLoop>();


                // Add above profiles

                profiles.Add(bottomProfile);
                profiles.Add(topProfile);

                // which value to be set exactly? He tried 0 and 1.

                List<double> pathParams = new List<double>();

                pathParams.Add(pathCurve.GetEndParameter(0));
                pathParams.Add(pathCurve.GetEndParameter(1));

                // Create a swept blend geometry.

                Solid solid = GeometryCreationUtilities.CreateSweptBlendGeometry(pathCurve, pathParams, profiles, null);

                List<GeometryObject> gs = new List<GeometryObject>();
                gs.Add(solid);
                directShape.AppendShape(gs);





                transaction.Commit();
            }
            return Result.Succeeded;



        }



        [Obsolete("Testing out how Revit transactions work")]
        public Result ExecuteWallTest(ExternalCommandData commandData, ref string message, ElementSet elements)
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

        [Obsolete("Testing out how Revit transactions work")]
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
