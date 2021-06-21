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

        public static IfcIndexedPolyCurve ToIfcIndexedPolyCurve(this GeometryElement geometryElement, bool closed, IfcStore model)
        {
            var segments = geometryElement.ToList();


            var points = new List<List<double>>();
            var indices = new List<List<IfcPositiveInteger>>();

            var firstSegment = segments[0] as Curve;

            // Calculating the start point of the whole polyline
            if (segments.Count > 1)
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
                    var cadPoint = firstSegment.GetEndPoint(0);

                    points.Add(new List<double>(){
                        cadPoint.X,
                        cadPoint.Y,
                        cadPoint.Z
                    });
                }
                else
                {
                    var cadPoint = firstSegment.GetEndPoint(1);

                    points.Add(new List<double>(){
                        cadPoint.X,
                        cadPoint.Y,
                        cadPoint.Z
                    });
                }

            }
            else
            {
                var cadPoint = firstSegment.GetEndPoint(0);

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

                    if (segment is Arc)
                    {
                        //var arc = segment as Arc;
                        //var midPoint = arc.GetPointAtDist(arc.Length / 2);

                        //var cadPoint = midPoint.TransformBy(transformMatrix);

                        //points.Add(new List<double>(){
                        //    cadPoint.X,
                        //    cadPoint.Y,
                        //    cadPoint.Z
                        //});

                        //segmentIndices.Add(points.Count);

                        //cadPoint = segment.StartPoint.TransformBy(transformMatrix);

                        //var p0 = new List<double>(){
                        //    cadPoint.X,
                        //    cadPoint.Y,
                        //    cadPoint.Z
                        //};


                        //cadPoint = segment.EndPoint.TransformBy(transformMatrix);
                        //var p1 = new List<double>(){
                        //    cadPoint.X,
                        //    cadPoint.Y,
                        //    cadPoint.Z
                        //};

                        //points.Add(IfcModelCreator.FartherPoint(points[points.Count - 2], p1, p0));

                    }

                    else if (segment is Line line)
                    {
                        var cadPoint = line.GetEndPoint(1);

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
