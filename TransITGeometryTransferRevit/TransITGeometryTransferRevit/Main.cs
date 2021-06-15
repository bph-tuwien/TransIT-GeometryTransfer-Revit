using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using CreationApplication = Autodesk.Revit.Creation.Application;
using FamilyItemFactory = Autodesk.Revit.Creation.FamilyItemFactory;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB.Structure;

using Xbim.Ifc;
using Xbim.Ifc4.GeometryResource;
using Xbim.Ifc4.Kernel;
using Xbim.Ifc4.RepresentationResource;
using Xbim.Ifc4.GeometricModelResource;

using TransITGeometryTransferRevit.Ifc.GeometryResource;


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
        const string _family_name = "TunnelProfile";

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




        private Family LoadFamilyIfNotLoaded(Document doc, string filename, string familyName)
        {
            // before loading the family, it needs to be checked wheter it is already loaded or not

            Family family = null;

            FilteredElementCollector a = new FilteredElementCollector(doc).OfClass(typeof(Family));

            int n = a.Count<Element>(e => e.Name.Equals(familyName));

            if (0 < n)
            {
                family = a.First<Element>(e => e.Name.Equals(familyName)) as Family;
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

        private string CreateTunnelProfileFamily(ExternalCommandData commandData, IfcRepresentationItem profileIfcRepresentationItem)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Application app = uiapp.Application;
            Document doc = uidoc.Document;

            if (null == doc)
            {
                throw new ArgumentNullException("Current document is null");
            }

            string templateFileName = Path.Combine(_path, _family_template_name + _family_template_ext);

            //Document fdoc = app.NewFamilyDocument(templateFileName);
            Document fdoc = app.OpenDocumentFile("Y:/RevitTunnel/TunnelSection/ProfileTemplateFamily.rfa");

            if (null == fdoc)
            {
                throw new ArgumentNullException("Cannot create family document");
            }


            Transaction revitTransaction = new Transaction(fdoc, "Creating tunnel profile family");
            {
                revitTransaction.Start();

                //CreateExtrusion(fdoc, _countour, _thicknessMm);

                var ifcProfileCurve = profileIfcRepresentationItem as IfcIndexedPolyCurve;
                var revitProfile = ifcProfileCurve.ToCurve();
                //var offsetCurve = ifcProfileCurve.ToCurve(Constants.MeterToFeet, -revitProfile.GetEndPoint(0) * Constants.MeterToFeet);
                var offsetCurve = ifcProfileCurve.ToCurve(Constants.MeterToFeet);



                IfcCartesianPointList3D pointList = ifcProfileCurve.Points as IfcCartesianPointList3D;
                var coordList = pointList.CoordList;



                //var offsetCurveArray = ifcProfileCurve.ToCurveArray(Constants.MeterToFeet, -revitProfile.GetEndPoint(0) * Constants.MeterToFeet);
                var offsetCurveArray = ifcProfileCurve.ToCurveArray(Constants.MeterToFeet);

                var curveArrayProfile = new CurveArray();
                curveArrayProfile.Append(offsetCurve);


                // https://forums.autodesk.com/t5/revit-api-forum/3d-model-line/td-p/5961937

                //var plane =  Plane.CreateByThreePoints(_countour[0], _countour[1], _countour[2]);
                // TODO: Fix this can be colinear

                var asd = curveArrayProfile.get_Item(0).Tessellate();


                //var asd2 = curveArrayProfile.get_Item(0).
                // TODO: Fix this
                //var plane =  Plane.CreateByThreePoints(asd[0], asd[100], asd[200]);
                var nurb = offsetCurve as NurbSpline;

                var nonColinearPoints = GetThreeNonColinearPoints(coordList.ToXYZArray());

                var plane = Plane.CreateByThreePoints(nonColinearPoints[0], nonColinearPoints[1], nonColinearPoints[2]);


                SketchPlane skp = SketchPlane.Create(fdoc, plane);
                ModelCurveArray mc = fdoc.FamilyCreate.NewModelCurveArray(offsetCurveArray, skp);



                //fdoc.get_Parameter(BuiltInParameter.FAMILY_WORK_PLANE_BASED).Set(0);
                fdoc.OwnerFamily.get_Parameter(BuiltInParameter.FAMILY_WORK_PLANE_BASED).Set(1);
                fdoc.OwnerFamily.get_Parameter(BuiltInParameter.FAMILY_SHARED).Set(1);
                fdoc.OwnerFamily.get_Parameter(BuiltInParameter.FAMILY_ALWAYS_VERTICAL).Set(0);



                revitTransaction.Commit();
            }

            string filename = Path.Combine(Path.GetTempPath(), _family_name + _rfa_ext);

            SaveAsOptions opt = new SaveAsOptions();
            opt.OverwriteExistingFile = true;

            fdoc.SaveAs(filename, opt);

            fdoc.Close(false);

            return filename;
        }

        

        public static XYZ[] GetThreeNonColinearPoints(XYZ[] coordList)
        {
            // TODO: Check if length is > 3

            for (int i = 0; i < coordList.Length; i++)
            {
                for (int j = i+1; j < coordList.Length; j++)
                {
                    for (int k = j+1; k < coordList.Length; k++)
                    {
                        var points = new XYZ[] { coordList[i], coordList[j], coordList[k] };
                        if (!IsColinear(points))
                        {
                            return points;
                        }
                    }
                }
            }

            throw new ArgumentException("Provided coordlist is fully colinear");
        }

        public static bool IsColinear(XYZ[] points)
        {
            // TODO: Check Lenght == 3
            return IsColinear(points[0], points[1], points[2]);
        }

            public static bool IsColinear(XYZ p1, XYZ p2, XYZ p3)
        {
            var d1 = (p1 - p2).GetLength();
            var d2 = (p1 - p3).GetLength();
            var d3 = (p2 - p3).GetLength();

            if (d1 + d2 <= d3)
            {
                return true;
            }
            if (d1 + d3 <= d2)
            {
                return true;
            }
            if (d2 + d3 <= d1)
            {
                return true;
            }

            return false;
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



            string filename = null;


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
                        if (obj.Representation == null || obj.Representation.Representations.Count != 4)
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

                            if (representation.RepresentationIdentifier == "Reference")
                            {
                                profileRepresentation = representation;
                            }
                        }

                        IfcIndexedPolyCurve ifcTunnelLine = axisRepresentation.Items[0] as IfcIndexedPolyCurve;

                        filename = CreateTunnelProfileFamily(commandData, profileRepresentation.Items[0]);

                        List<CurveLoop> revitProfiles = new List<CurveLoop>();

                        foreach (var ifcProfileItem in profileRepresentation.Items)
                        {
                            var ifcProfileCurve = ifcProfileItem as IfcIndexedPolyCurve;
                            var revitProfile = ifcProfileCurve.ToCurveLoop();
                            revitProfiles.Add(revitProfile);
                        }

                        var revitTunnelLine = ifcTunnelLine.ToCurve();



                    }
                }
            }











            // Loading Profile Family
            ElementId profileInstanceElementId = null;


            var revitTransaction = new Transaction(doc, "Inserting tunnel profile family instance");
            {
                revitTransaction.Start();


                Family family = LoadFamilyIfNotLoaded(doc, filename, _family_name);


                FamilySymbol symbol = GetFirstFamilySymbol(family);

                // Make sure to activate symbol
                if (!symbol.IsActive)
                { symbol.Activate(); doc.Regenerate(); }


                XYZ p = new XYZ(10, 10, 0);
                StructuralType st = StructuralType.UnknownFraming;

                //var profileInstance = doc.Create.NewFamilyInstance(p, symbol, st);
                //var profileInstance = doc.FamilyCreate.NewFamilyInstance(p, symbol, st);
                profileInstanceElementId = symbol.Id;


                revitTransaction.Commit();
            }



            // Loading Tunnel Section Family







            // Creating tunnel line and placing tunnel sections on it


            XYZ[] pts = new XYZ[0];
            Curve revitTunnelLine1 = null;

            revitTransaction = new Transaction(doc);
            {
                revitTransaction.Start("Importing 3d tunnel");


                using (var model = IfcStore.Open(ifcFileInfo.FullName))
                {
                    using (var ifcTransaction = model.BeginTransaction("Reading 3d tunnel to recreate in Revit"))
                    {

                        var objects = model.Instances.OfType<IfcProduct>();

                        foreach (var obj in objects)
                        {

                            // TODO: Refactor this, should be a better way of finding the tunnel
                            if (obj.Representation == null || obj.Representation.Representations.Count != 4)
                            {
                                continue;
                            }

                            var representations = obj.Representation.Representations;


                            IfcRepresentation axisRepresentation = null;

                            foreach (var representation in representations)
                            {
                                if (representation.RepresentationIdentifier == "Axis")
                                {
                                    axisRepresentation = representation;
                                }

                            }

                            IfcIndexedPolyCurve ifcTunnelLine = axisRepresentation.Items[0] as IfcIndexedPolyCurve;


                            Curve revitTunnelLine = ifcTunnelLine.ToCurve();
                            revitTunnelLine1 = ifcTunnelLine.ToCurve(Constants.MeterToFeet, -revitTunnelLine.GetEndPoint(0) * Constants.MeterToFeet);
                            CurveArray revitTunnelLine2 = ifcTunnelLine.ToCurveArray();
                            CurveLoop revitTunnelLine3 = ifcTunnelLine.ToCurveLoop();

                            // https://thebuildingcoder.typepad.com/blog/2013/11/placing-equidistant-points-along-a-curve.html




                            pts = CreateEquiDistantPointsOnCurve(revitTunnelLine1);


                           

                            //revitTunnelLine1.Refe

                            
                            // Place a marker circle at each point.


                            //foreach (XYZ pt in pts)
                            //{
                            //    CreateCircle(doc, pt, 1);
                            //}








                            ;

                            //revitTunnelLine1.

                            List<double> pathParams = new List<double>();

                            // TODO: FIX THIS - Profile placement is NOT implemented! Arbitrary values
                            //pathParams.Add(revitTunnelLine.GetEndParameter(0));
                            //pathParams.Add(revitTunnelLine.GetEndParameter(1));


                            ;


                        }
                    }
                }

                revitTransaction.Commit();
            }


            // Create tunnel sections

            revitTransaction = new Transaction(doc, "Inserting tunnel section family instance");
            {
                revitTransaction.Start();


                Family family = LoadFamilyIfNotLoaded(doc, "Y:/RevitTunnel/TunnelSection/TunnelSectionFamily.rfa", "TunnelSectionFamily");


                FamilySymbol symbol = GetFirstFamilySymbol(family);

                // Make sure to activate symbol
                if (!symbol.IsActive)
                { symbol.Activate(); doc.Regenerate(); }


                XYZ p = new XYZ(0, 0, 0);
                StructuralType st = StructuralType.UnknownFraming;

                //doc.Create.NewFamilyInstance(p, symbol, st);
                //AdaptiveComponentInstanceUtils.CreateAdaptiveComponentInstance(doc, symbol);


                for (int i = 1; i  < pts.Length; i++)
                //for (int i = 1; i  < 2; i++)
                {
                    var instance = CreateAdaptiveComponentInstance(doc, symbol, new XYZ[] { pts[i - 1], pts[i] });





                    // https://www.youtube.com/watch?v=sZWSQJWVhbY


                    //Parameter myparam = instance.LookupParameter("TestParam");
                    Parameter myparam = instance.LookupParameter("Profile");

                    var asd = myparam.AsDouble();
                    var asd2 = myparam.AsElementId();
                    var asd3 = myparam.AsInteger();
                    var asd4 = myparam.AsString();
                    var asd5 = myparam.AsValueString();

                    ;

                    //myparam.SetValueString("Tunnel Profile");
                    //myparam.SetValueString("69");
                    //myparam.Set("TunnelProfile");
                    myparam.Set(profileInstanceElementId);

                    var asd6 = myparam.AsValueString();


                    ;
                }







                //var location1 = new PointLocationOnCurve(PointOnCurveMeasurementType.NormalizedCurveParameter, 0f, PointOnCurveMeasureFrom.Beginning);
                //var location2 = new PointLocationOnCurve(PointOnCurveMeasurementType.NormalizedCurveParameter, 0.1, PointOnCurveMeasureFrom.Beginning);
                //var location3 = new PointLocationOnCurve(PointOnCurveMeasurementType.NormalizedCurveParameter, 0.2, PointOnCurveMeasureFrom.Beginning);

                //var pointOnEdge1 = doc.Application.Create.NewPointOnEdge(revitTunnelLine1.Reference, location1);
                //var pointOnEdge2 = doc.Application.Create.NewPointOnEdge(revitTunnelLine1.Reference, location2);
                //var pointOnEdge3 = doc.Application.Create.NewPointOnEdge(revitTunnelLine1.Reference, location3);


                //// Create a new instance of an adaptive component family
                //FamilyInstance instance = AdaptiveComponentInstanceUtils.CreateAdaptiveComponentInstance(doc, symbol);


                //var placementPoints = AdaptiveComponentInstanceUtils.GetInstancePlacementPointElementRefIds(instance);



                //var p1 = doc.GetElement(placementPoints[0]) as ReferencePoint;
                //var p2 = doc.GetElement(placementPoints[1]) as ReferencePoint;
                //var p3 = doc.GetElement(placementPoints[2]) as ReferencePoint;

                //p1.SetPointElementReference(pointOnEdge1);
                //p2.SetPointElementReference(pointOnEdge2);
                //p3.SetPointElementReference(pointOnEdge3);












                revitTransaction.Commit();
            }




            return Result.Succeeded;

        }



        public static XYZ[] CreateEquiDistantPointsOnCurve(Curve curve)
        {

            IList<XYZ> tessellation = curve.Tessellate();

            // Create a list of equi-distant points.

            List<XYZ> pts = new List<XYZ>(1);

            // TODO: Change it back to original
            //double stepsize = 1.0 * Constants.MeterToFeet;
            double stepsize = 10.0 * Constants.MeterToFeet;
            double dist = 0.0;

            XYZ p = curve.GetEndPoint(0);

            foreach (XYZ q in tessellation)
            {
                if (0 == pts.Count)
                {
                    pts.Add(p);
                    dist = 0.0;
                }
                else
                {
                    dist += p.DistanceTo(q);

                    if (dist >= stepsize)
                    {
                        pts.Add(q);
                        dist = 0;
                    }
                    p = q;
                }
            }

            return pts.ToArray();
        }


        /// <summary>
        /// Create a horizontal detail curve circle of 
        /// the given radius at the specified point.
        /// </summary>
        DetailArc CreateCircle(
          Document doc,
          XYZ location,
          double radius)
        {
            XYZ norm = XYZ.BasisZ;

            double startAngle = 0;
            double endAngle = 2 * Math.PI;

            var plane = Plane.CreateByNormalAndOrigin(norm, location);


            Arc arc = Arc.Create(plane,
              radius, startAngle, endAngle);

            return doc.Create.NewDetailCurve(
              doc.ActiveView, arc) as DetailArc;
        }


        private FamilyInstance CreateAdaptiveComponentInstance(Document document, FamilySymbol symbol, XYZ[] points)
        {
            // Create a new instance of an adaptive component family
            FamilyInstance instance = AdaptiveComponentInstanceUtils.CreateAdaptiveComponentInstance(document, symbol);

            // Get the placement points of this instance
            IList<ElementId> placePointIds = new List<ElementId>();
            placePointIds = AdaptiveComponentInstanceUtils.GetInstancePlacementPointElementRefIds(instance);
            double x = 0;

            // Set the position of each placement point
            //foreach (ElementId id in placePointIds)
            //{
            //    ReferencePoint point = document.GetElement(id) as ReferencePoint;
            //    point.Position = new Autodesk.Revit.DB.XYZ(30 * x, 30 * Math.Cos(x), 0);
            //    x += Math.PI / 6;
            //}

            // https://forums.autodesk.com/t5/revit-api-forum/edge-reference-of-a-family-instance/td-p/7088651

            for (int i = 0; i < 2; i++)
            {
                ReferencePoint refPoint = document.GetElement(placePointIds[i]) as ReferencePoint;
                refPoint.Position = points[i];
                //refPoint.SetPointElementReference()
            }




            var lineVector = points[1] - points[0];
            var plane = Plane.CreateByThreePoints(points[0], points[1], points[0] + new XYZ(0, 0, 10000 * Constants.MillimeterToFeet));
            var normal = plane.Normal.Normalize();


            ReferencePoint refPoint1 = document.GetElement(placePointIds[2]) as ReferencePoint;
            refPoint1.Position = points[0] + new XYZ(0,0,10000 * Constants.MillimeterToFeet);

            ReferencePoint refPoint2 = document.GetElement(placePointIds[3]) as ReferencePoint;
            refPoint2.Position = points[0] + normal * 10000 * Constants.MillimeterToFeet;


            ReferencePoint refPoint3 = document.GetElement(placePointIds[4]) as ReferencePoint;
            refPoint3.Position = points[1] + new XYZ(0, 0, 10000 * Constants.MillimeterToFeet);

            ReferencePoint refPoint4 = document.GetElement(placePointIds[5]) as ReferencePoint;
            refPoint4.Position = points[1] + normal * 10000 * Constants.MillimeterToFeet;


            return instance;
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
