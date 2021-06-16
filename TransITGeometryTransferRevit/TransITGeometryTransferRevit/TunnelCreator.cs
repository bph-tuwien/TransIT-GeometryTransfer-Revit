using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
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
    class TunnelCreator
    {


        /// <summary>
        /// Creates a Generic Model family based on the given IfcRepresentationItem that represents the profile of the
        /// tunnel.
        /// </summary>
        /// <param name="commandData">ExternalCommandData of the Revit plugin execution</param>
        /// <param name="ifcProfileCurve">The curve representing the tunnel's profile</param>
        /// <returns>The path to the generated profile family</returns>
        public static string CreateTunnelProfileFamily(ExternalCommandData commandData, IfcIndexedPolyCurve ifcProfileCurve)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Application app = uiapp.Application;
            Document doc = uidoc.Document;


            if (null == doc)
            {
                throw new ArgumentNullException("Current document is null");
            }

            var profileTemplateFamilyPath = TemplateFamiliesBase64.GetBase64FamilyPath(TemplateFamiliesBase64.profileFamilyTemplateBase64);
            Document fdoc = app.OpenDocumentFile(profileTemplateFamilyPath);

            if (null == fdoc)
            {
                throw new ArgumentNullException("Cannot open template family document");
            }


            Transaction revitTransaction = new Transaction(fdoc, "Creating tunnel profile family");
            {
                revitTransaction.Start();

                IfcCartesianPointList3D pointList = ifcProfileCurve.Points as IfcCartesianPointList3D;
                var coordList = pointList.CoordList;


                var revitCurveArray = ifcProfileCurve.ToCurveArray(Constants.MeterToFeet);


                var nonColinearPoints = GetThreeNonColinearPoints(coordList.ToXYZArray());
                var plane = Plane.CreateByThreePoints(nonColinearPoints[0], nonColinearPoints[1], nonColinearPoints[2]);
                SketchPlane skp = SketchPlane.Create(fdoc, plane);
                ModelCurveArray mc = fdoc.FamilyCreate.NewModelCurveArray(revitCurveArray, skp);


                fdoc.OwnerFamily.get_Parameter(BuiltInParameter.FAMILY_WORK_PLANE_BASED).Set(1);
                fdoc.OwnerFamily.get_Parameter(BuiltInParameter.FAMILY_SHARED).Set(1);
                fdoc.OwnerFamily.get_Parameter(BuiltInParameter.FAMILY_ALWAYS_VERTICAL).Set(0);


                revitTransaction.Commit();
            }

            string filename = Path.Combine(Path.GetTempPath(), "TunnelProfile.rfa");

            SaveAsOptions opt = new SaveAsOptions();
            opt.OverwriteExistingFile = true;
            fdoc.SaveAs(filename, opt);
            fdoc.Close(false);

            return filename;
        }



        /// <summary>
        /// Finds 3 non-colinear points in a list of points. Throws exception if such points could not be found.
        /// </summary>
        /// <param name="coordList">The list of points to find the non_colinear points in</param>
        /// <returns>3 non-colinear points from the given coordList or an exception if could not be found</returns>
        public static XYZ[] GetThreeNonColinearPoints(XYZ[] coordList)
        {
            if (coordList.Count() < 3)
            {
                throw new ArgumentException("coordList has to contain at least 3 points to have a non-colinear trio");
            }

            for (int i = 0; i < coordList.Length; i++)
            {
                for (int j = i + 1; j < coordList.Length; j++)
                {
                    for (int k = j + 1; k < coordList.Length; k++)
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

        /// <summary>
        /// Checks if the 3 points colinearity by the triangle sides' length and triangle inequality
        /// </summary>
        /// <param name="points"></param>
        /// <returns>The colinearity of the 3 points</returns>
        public static bool IsColinear(XYZ[] points)
        {
            // TODO: Check Lenght == 3
            if (points.Length != 3)
            {
                throw new ArgumentOutOfRangeException("Colinearity check requires exactly 3 points");
            }

            return IsColinear(points[0], points[1], points[2]);
        }

        /// <summary>
        /// Checks if the 3 points colinearity by the triangle sides' length and triangle inequality
        /// </summary>
        /// <param name="p1"></param>
        /// <param name="p2"></param>
        /// <param name="p3"></param>
        /// <returns>The colinearity of the 3 points</returns>
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


    }
}
