using System;
using System.Collections.Generic;
using System.IO;

using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.ApplicationServices;

using Xbim.Ifc;
using Xbim.Ifc4.GeometryResource;
using Xbim.Ifc4.Kernel;
using Xbim.Ifc4.RepresentationResource;

using TransITGeometryTransferRevit.Ifc.GeometryResource;


namespace TransITGeometryTransferRevit.Commands
{

    /// <summary>
    /// The class containing the callable Revit commands.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class ImportAndGenerateTunnel : IExternalCommand
    {
        /// <summary>
        /// The length of the generated Tunnel Sections
        /// </summary>
        public const float TUNNEL_SECTION_LENGTH = 1.0f;

        /// <summary>
        /// Entry point of the family based tunnel import and generation plugin. 
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

            string tunnelProfileFamilyPath = null;


            var ifcFilePath = UserInteractions.PromptIfcFileOpenDialog();
            FileInfo ifcFileInfo = new FileInfo(ifcFilePath);


            // #################################################################################
            // CREATING TUNNEL PROFILE FAMILY BASED ON THE IFC TUNNEL'S REFERENCE REPRESENTATION
            // #################################################################################

            using (var ifcModel = IfcStore.Open(ifcFileInfo.FullName))
            {
                using (var ifcTransaction = ifcModel.BeginTransaction("Parsing Tunnel to recreate Profile as a Revit Family"))
                {
                    var ifcTunnel = TunnelCreator.GetTunnelIfcProduct(ifcModel);

                    if (ifcTunnel == null)
                    {
                        throw new NullReferenceException("Could not found Tunnel IfcProduct in the model");
                    }

                    var representations = ifcTunnel.Representation.Representations;

                    IfcRepresentation referenceRepresentation = null;

                    foreach (var representation in representations)
                    {
                        if (representation.RepresentationIdentifier == "Reference")
                        {
                            referenceRepresentation = representation;
                        }
                    }

                    if (referenceRepresentation == null)
                    {
                        throw new NullReferenceException("Tunnel IfcProduct has no Reference representation");
                    }

                    if (referenceRepresentation.Items.Count == 0)
                    {
                        throw new NullReferenceException("Tunnel IfcProduct's Reference representation has no Items");
                    }

                    var ifcTunnelLine = referenceRepresentation.Items[0] as IfcIndexedPolyCurve;
                    tunnelProfileFamilyPath = TunnelCreator.CreateTunnelProfileFamily(commandData, ifcTunnelLine);

                }
            }


            // #######################################
            // LOADING GENERATED TUNNEL PROFILE FAMILY
            // #######################################

            ElementId tunnelProfileFamilySymbolId = null;

            var revitTransaction = new Transaction(doc, "Loading tunnel profile family");
            {
                revitTransaction.Start();


                Family family = FamilyUtils.LoadFamilyIfNotLoaded(doc, tunnelProfileFamilyPath, "TunnelProfile");
                FamilySymbol symbol = FamilyUtils.GetFirstFamilySymbol(family);

                if (!symbol.IsActive)
                { symbol.Activate(); doc.Regenerate(); }

                tunnelProfileFamilySymbolId = symbol.Id;


                revitTransaction.Commit();
            }

            // #########################################################
            // IMPORTING TUNNEL LINE AND RECREATING IT AS A DIRECT SHAPE
            // #########################################################

            revitTransaction = new Transaction(doc);
            {
                revitTransaction.Start("Importing 3d tunnel");


                using (var model = IfcStore.Open(ifcFileInfo.FullName))
                {
                    using (var ifcTransaction = model.BeginTransaction("Reading 3d tunnel to recreate in Revit"))
                    {
                        var ifcTunnel = TunnelCreator.GetTunnelIfcProduct(model);

                        if (ifcTunnel == null)
                        {
                            throw new NullReferenceException("Could not found Tunnel IfcProduct in the model");
                        }

                        var representations = ifcTunnel.Representation.Representations;

                        IfcRepresentation axisRepresentation = null;

                        foreach (var representation in representations)
                        {
                            if (representation.RepresentationIdentifier == "Axis")
                            {
                                axisRepresentation = representation;
                            }
                        }

                        if (axisRepresentation == null)
                        {
                            throw new NullReferenceException("Tunnel IfcProduct has no Reference representation");
                        }

                        if (axisRepresentation.Items.Count == 0)
                        {
                            throw new NullReferenceException("Tunnel IfcProduct's Reference representation has no Items");
                        }

                        IfcIndexedPolyCurve ifcTunnelLine = axisRepresentation.Items[0] as IfcIndexedPolyCurve;

                        Curve tempTunnelLine = ifcTunnelLine.ToCurve();
                        var tunnelLine = ifcTunnelLine.ToCurveArray(Constants.MeterToFeet,
                                                                -tempTunnelLine.GetEndPoint(0) * Constants.MeterToFeet);

                        WireframeBuilder builder = new WireframeBuilder();
                        

                        foreach (Curve curve in tunnelLine)
                        {
                            builder.AddCurve(curve);
                        }

                        ElementId categoryId = new ElementId(BuiltInCategory.OST_GenericModel);

                        DirectShape ds = DirectShape.CreateElement(doc, categoryId);

                        ds.SetShape(builder);

                        ds.Name = "TunnelLine";
                    }
                }

                revitTransaction.Commit();
            }


            // ####################################################################
            // IMPORTING TUNNEL LINE AND CALCULATING EQUIDISTANT POINTS ON THE LINE
            // ####################################################################

            XYZ[] pointsOnTunnelLine = new XYZ[0];
            Curve revitTunnelLine = null;

            revitTransaction = new Transaction(doc);
            {
                revitTransaction.Start("Importing 3d tunnel");


                using (var model = IfcStore.Open(ifcFileInfo.FullName))
                {
                    using (var ifcTransaction = model.BeginTransaction("Reading 3d tunnel to recreate in Revit"))
                    {
                        var ifcTunnel = TunnelCreator.GetTunnelIfcProduct(model);

                        if (ifcTunnel == null)
                        {
                            throw new NullReferenceException("Could not found Tunnel IfcProduct in the model");
                        }

                        var representations = ifcTunnel.Representation.Representations;

                        IfcRepresentation axisRepresentation = null;

                        foreach (var representation in representations)
                        {
                            if (representation.RepresentationIdentifier == "Axis")
                            {
                                axisRepresentation = representation;
                            }
                        }

                        if (axisRepresentation == null)
                        {
                            throw new NullReferenceException("Tunnel IfcProduct has no Reference representation");
                        }

                        if (axisRepresentation.Items.Count == 0)
                        {
                            throw new NullReferenceException("Tunnel IfcProduct's Reference representation has no Items");
                        }

                        IfcIndexedPolyCurve ifcTunnelLine = axisRepresentation.Items[0] as IfcIndexedPolyCurve;

                        Curve tempTunnelLine = ifcTunnelLine.ToCurve();
                        revitTunnelLine = ifcTunnelLine.ToCurve(Constants.MeterToFeet,
                                                                -tempTunnelLine.GetEndPoint(0) * Constants.MeterToFeet);

                        // TODO: Use maybe arc.Evaluate() instead?
                        pointsOnTunnelLine = TunnelCreator.CreateEquiDistantPointsOnCurve(revitTunnelLine, 
                                                                        TUNNEL_SECTION_LENGTH * Constants.MeterToFeet);

                    }
                }

                revitTransaction.Commit();
            }


            // ########################################################
            // LOADING, INSTATIATING, AND SETTING TUNNEL SECTION FAMILY
            // ########################################################

            revitTransaction = new Transaction(doc, "Inserting tunnel section family instance");
            {
                revitTransaction.Start();

                var tunnelSectionTemplateFamilyPath = TemplateFamiliesBase64.GetBase64FamilyPath(
                                                            TemplateFamiliesBase64.tunnelSectionFamilyTemplateBase64);

                Family family = FamilyUtils.LoadFamilyIfNotLoaded(doc, tunnelSectionTemplateFamilyPath,
                                                                  "TunnelSectionFamily");
                family.Name = "TunnelSectionFamily";
                FamilySymbol symbol = FamilyUtils.GetFirstFamilySymbol(family);
                symbol.Name = "TunnelSectionFamily";

                if (!symbol.IsActive)
                { symbol.Activate(); doc.Regenerate(); }

                for (int i = 1; i < pointsOnTunnelLine.Length; i++)
                {
                    var sectionPoints = new XYZ[] { pointsOnTunnelLine[i - 1], pointsOnTunnelLine[i] };
                    var instance = TunnelCreator.CreateTunnelSectionInstance(doc, symbol, sectionPoints);

                    Parameter profileParam = instance.LookupParameter("Profile");
                    profileParam.Set(tunnelProfileFamilySymbolId);

                    Parameter sectionIDParam = instance.LookupParameter("SectionID");
                    sectionIDParam.Set(i-1);

                }

                revitTransaction.Commit();
            }


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
