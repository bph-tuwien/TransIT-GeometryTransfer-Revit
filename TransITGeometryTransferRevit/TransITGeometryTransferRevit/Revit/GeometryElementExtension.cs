using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xbim.Ifc;
using Xbim.Ifc4.GeometricModelResource;
using Xbim.Ifc4.GeometryResource;
using Xbim.Ifc4.MeasureResource;

namespace TransITGeometryTransferRevit.Revit
{
    public static class GeometryElementExtension
    {

        public static IfcIndexedPolyCurve ToIfcIndexedPolyCurve(this CurveArray curveArray, bool closed, IfcStore model, Transform transform, double unitConversion)
        {
            var segmentList = new List<Curve>();

            foreach (Curve curve in curveArray)
            {
                segmentList.Add(curve);
            }

            var segments = segmentList.ToArray();

            return segments.ToIfcIndexedPolyCurve(closed, model, transform, unitConversion);

        }

        public static IfcIndexedPolyCurve ToIfcIndexedPolyCurve(this GeometryElement geometryElement, bool closed, IfcStore model, Transform transform, double unitConversion)
        {
            var geometryObjectList = geometryElement.ToList();

            var segmentList = new List<Curve>();


            foreach (Curve curve in geometryObjectList)
            {
                segmentList.Add(curve);
            }

            var segments = segmentList.ToArray();

            return segments.ToIfcIndexedPolyCurve(closed, model, transform, unitConversion);

        }

        private static List<double> FartherPoint(List<double> measuredFrom, List<double> p0, List<double> p1)
        {
            // TODO: Make it work for any dimensions
            var sd0 = Math.Pow(p0[0] - measuredFrom[0], 2) +
                      Math.Pow(p0[1] - measuredFrom[1], 2) +
                      Math.Pow(p0[2] - measuredFrom[2], 2);

            var sd1 = Math.Pow(p1[0] - measuredFrom[0], 2) +
                      Math.Pow(p1[1] - measuredFrom[1], 2) +
                      Math.Pow(p1[2] - measuredFrom[2], 2);

            return sd0 > sd1 ? p0 : p1;
        }

        private static IfcIndexedPolyCurve ToIfcIndexedPolyCurve(this Curve[] segments, bool closed, IfcStore model, Transform transform, double unitConversion)
        {

            var points = new List<List<double>>();
            var indices = new List<List<IfcPositiveInteger>>();

            var firstSegment = segments[0] as Curve;

            // Calculating the start point of the whole polyline
            if (segments.Length > 1)
            {
                var secondSegment = segments[1] as Curve;

                var p0 = firstSegment.GetEndPoint(0);
                var p1 = firstSegment.GetEndPoint(1);

                double p0_Distance = p0.DistanceTo(secondSegment.GetEndPoint(0));
                if (p0_Distance > p0.DistanceTo(secondSegment.GetEndPoint(1)))
                {
                    p0_Distance = p0.DistanceTo(secondSegment.GetEndPoint(1));
                }

                double p1_Distance = p1.DistanceTo(secondSegment.GetEndPoint(0));
                if (p1_Distance > p1.DistanceTo(secondSegment.GetEndPoint(1)))
                {
                    p1_Distance = p1.DistanceTo(secondSegment.GetEndPoint(1));
                }

                if (p0_Distance > p1_Distance)
                {
                    var cadPoint = transform.OfPoint(firstSegment.GetEndPoint(0)) * unitConversion;

                    points.Add(new List<double>(){
                        cadPoint.X,
                        cadPoint.Y,
                        cadPoint.Z
                    });
                }
                else
                {
                    var cadPoint = transform.OfPoint(firstSegment.GetEndPoint(1)) * unitConversion;

                    points.Add(new List<double>(){
                        cadPoint.X,
                        cadPoint.Y,
                        cadPoint.Z
                    });
                }

            }
            else
            {
                var cadPoint = transform.OfPoint(firstSegment.GetEndPoint(0)) * unitConversion;

                points.Add(new List<double>(){
                        cadPoint.X,
                        cadPoint.Y,
                        cadPoint.Z
                    });
            }




            IfcIndexedPolyCurve ifcIndexedPolyCurve = model.Instances.New<IfcIndexedPolyCurve>(ifcCurve =>
            {

                foreach (var segment in segments)
                {

                    var segmentIndices = new List<IfcPositiveInteger>();
                    segmentIndices.Add(points.Count);

                    // TODO: Fix Arcs
                    if (segment is Arc arc2)
                    {
                        var cadPoint = transform.OfPoint(arc2.GetEndPoint(1)) * unitConversion;

                        points.Add(new List<double>(){
                            cadPoint.X,
                            cadPoint.Y,
                            cadPoint.Z
                        });
                    }
                    else if (segment is Arc)
                    {
                        var arc = segment as Arc;


                        var tessellatedArc = arc.Tessellate();


                        //var midPoint = tessellatedArc[tessellatedArc.Count/2];
                        var midPoint = tessellatedArc[1];

                        var cadPoint = transform.OfPoint(midPoint) * unitConversion;
                        var transMidPoint = cadPoint;

                        points.Add(new List<double>(){
                            cadPoint.X,
                            cadPoint.Y,
                            cadPoint.Z
                        });

                        segmentIndices.Add(points.Count);

                        cadPoint = transform.OfPoint(segment.GetEndPoint(0)) * unitConversion;
                        var transStartPoint = cadPoint;


                        var p0 = new List<double>(){
                            cadPoint.X,
                            cadPoint.Y,
                            cadPoint.Z
                        };


                        cadPoint = transform.OfPoint(segment.GetEndPoint(1)) * unitConversion;
                        var transEndPoint = cadPoint;


                        var p1 = new List<double>(){
                            cadPoint.X,
                            cadPoint.Y,
                            cadPoint.Z
                        };

                        points.Add(FartherPoint(points[points.Count - 2], p1, p0));

                    }

                    else if (segment is Line line)
                    {
                        var cadPoint = transform.OfPoint(line.GetEndPoint(1)) * unitConversion;

                        points.Add(new List<double>(){
                            cadPoint.X,
                            cadPoint.Y,
                            cadPoint.Z
                        });
                    }

                    segmentIndices.Add(points.Count);
                    indices.Add(segmentIndices);

                }


                ifcCurve.Points = model.Instances.New<IfcCartesianPointList3D>(coordinates =>
                {
                    for (int j = 0; j < points.Count; j++)
                    {
                        var values = points[j].Select(v => new IfcLengthMeasure(v));
                        coordinates.CoordList.GetAt(j).AddRange(values);
                    };
                });


                int i = 0;
                foreach (var segment in indices)
                {
                    if (segment.Count == 2)
                    {

                        if (closed && i == (indices.Count - 1))
                        {
                            segment.Add(1);
                            ifcCurve.Segments.Add(new IfcLineIndex(segment));
                        }
                        else
                        {
                            ifcCurve.Segments.Add(new IfcLineIndex(segment));
                        }

                    }

                    else if (segment.Count == 3)
                    {

                        ifcCurve.Segments.Add(new IfcArcIndex(segment));


                    }
                    i++;
                }

            });

            return ifcIndexedPolyCurve;


        }


    }
}
