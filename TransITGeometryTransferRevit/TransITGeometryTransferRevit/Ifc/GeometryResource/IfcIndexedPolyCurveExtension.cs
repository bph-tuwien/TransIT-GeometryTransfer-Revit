using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xbim.Common;
using Xbim.Ifc;
using Xbim.Ifc4.GeometricModelResource;
using Xbim.Ifc4.GeometryResource;
using Xbim.Ifc4.MeasureResource;

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

            var segment = ifcIndexedPolyCurve.Segments[0];

            ;




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



            while ((coordList[coordList.Count - 1].ToXYZ() - coordList[0].ToXYZ()).IsZeroLength())
            {
                coordList.RemoveAt(coordList.Count - 1);
            }


            CurveLoop profile = new CurveLoop();


            foreach (var segment in ifcIndexedPolyCurve.Segments)
            {
                var curve = segment.ToCurve(coordList);
                if (curve != null)
                {
                    profile.Append(curve);
                }
            }

            if (profile.IsOpen())
            {
                var startOfLoop = profile.First<Curve>();
                var endOfLoop = profile.Last<Curve>();

                var closingLine = Line.CreateBound(endOfLoop.GetEndPoint(1), startOfLoop.GetEndPoint(0));
                profile.Append(closingLine);

            }

            var asd = profile.IsOpen();
            ;

            //var segment = ifcIndexedPolyCurve.Segments[0];

            //;

            //segment.ToCurve(coordList);


            //foreach (var coord in coordList)
            //{
            //    points.Add(new XYZ(coord[0], coord[1], coord[2]));
            //}

            //while((points[points.Count - 1] - points[0]).IsZeroLength())
            //{
            //    ;
            //    points.RemoveAt(points.Count - 1);
            //}

            //// TODO: Clean up! It shouldn't be profile specific

            //for (int i = 0; i < points.Count-1; i++)
            //{
            //    profile.Append(Line.CreateBound(points[i], points[i+1]));
            //}


            //profile.Append(Line.CreateBound(points[points.Count-1], points[0]));

            return profile;
        }

        private static XYZ ToXYZ(this IItemSet<IfcLengthMeasure> coord)
        {
            // TODO: Length check
            return new XYZ(coord[0], coord[1], coord[2]);
        }

        private static Curve ToCurve(this IfcSegmentIndexSelect segment, IItemSet<IItemSet<IfcLengthMeasure>> coordList)
        {
            if (segment is IfcLineIndex line)
            {
                List<IfcPositiveInteger> indexes = line.Value as List<IfcPositiveInteger>;

                var indices = segment.Value as List<IfcPositiveInteger>;


                var startIndex = Convert.ToInt32((long)indices[0].Value) - 1;
                var endIndex = Convert.ToInt32((long)indices[1].Value) - 1;

                if (startIndex >= coordList.Count || endIndex >= coordList.Count)
                {
                    return null;
                }


                var startPoint = new XYZ(coordList[startIndex][0], coordList[startIndex][1], coordList[startIndex][2]);
                var endPoint = new XYZ(coordList[endIndex][0], coordList[endIndex][1], coordList[endIndex][2]);

                return Line.CreateBound(startPoint, endPoint);
            }

            if (segment is IfcArcIndex arc)
            {
                List<IfcPositiveInteger> indexes = arc.Value as List<IfcPositiveInteger>;

                var indices = segment.Value as List<IfcPositiveInteger>;


                var startIndex = Convert.ToInt32((long)indices[0].Value) - 1;
                var onArcIndex = Convert.ToInt32((long)indices[1].Value) - 1;
                var endIndex = Convert.ToInt32((long)indices[2].Value) - 1;

                if (startIndex >= coordList.Count || onArcIndex >= coordList.Count || endIndex >= coordList.Count)
                {
                    return null;
                }

                var startPoint = new XYZ(coordList[startIndex][0], coordList[startIndex][1], coordList[startIndex][2]);
                var onArcPoint = new XYZ(coordList[onArcIndex][0], coordList[onArcIndex][1], coordList[onArcIndex][2]);
                var endPoint = new XYZ(coordList[endIndex][0], coordList[endIndex][1], coordList[endIndex][2]);

                return Arc.Create(startPoint, endPoint, onArcPoint);

            }

            return null;

            // TODO: Do the rest of the types

        }


    }
}
