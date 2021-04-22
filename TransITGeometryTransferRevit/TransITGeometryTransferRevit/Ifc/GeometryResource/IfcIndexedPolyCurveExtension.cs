using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xbim.Ifc;
using Xbim.Ifc4.GeometricModelResource;
using Xbim.Ifc4.GeometryResource;

namespace TransITGeometryTransferRevit.Ifc.GeometryResource
{
    public static class IfcIndexedPolyCurveExtension
    {

        public static Curve ToCurve(this IfcIndexedPolyCurve ifcIndexedPolyCurve)
        {

            // Create a path curve
            List<XYZ> controlPoints = new List<XYZ>();
            List<double> weights = new List<double>();


            IfcCartesianPointList3D pointList = ifcIndexedPolyCurve.Points as IfcCartesianPointList3D;
            var coordList = pointList.CoordList;


            foreach (var coord in coordList)
            {
                controlPoints.Add(new XYZ(coord[0], coord[1], coord[2]));
                weights.Add(1.0);
            }

            Curve pathCurve = NurbSpline.CreateCurve(controlPoints, weights);


            return pathCurve;

        }

        public static CurveLoop ToCurveLoop(this IfcIndexedPolyCurve ifcIndexedPolyCurve)
        {

            List<XYZ> points = new List<XYZ>();

            IfcCartesianPointList3D pointList = ifcIndexedPolyCurve.Points as IfcCartesianPointList3D;
            var coordList = pointList.CoordList;


            foreach (var coord in coordList)
            {
                points.Add(new XYZ(coord[0], coord[1], coord[2]));
            }

            points.RemoveAt(points.Count - 1);

            // TODO: Clean up! It shouldn't be profile specific
            CurveLoop profile = new CurveLoop();

            for (int i = 0; i < points.Count-1; i++)
            {
                profile.Append(Line.CreateBound(points[i], points[i+1]));
            }


            profile.Append(Line.CreateBound(points[points.Count-1], points[0]));

            return profile;
        }


    }
}
