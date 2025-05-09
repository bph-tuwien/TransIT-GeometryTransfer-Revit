﻿using System;
using System.Collections.Generic;
using System.Linq;

using Autodesk.Revit.DB;

using Xbim.Ifc;
using Xbim.Ifc4.GeometricModelResource;
using Xbim.Ifc4.GeometryResource;
using Xbim.Ifc4.MeasureResource;
using Xbim.Common.Geometry;


namespace TransITGeometryTransferRevit.Revit
{
    /// <summary>
    /// Extension methods to convert various Revit curves to IfcIndexedPolyCurve.
    /// </summary>
    public static class GeometryExtension
    {
        /// <summary>
        /// Returns the point which is further from the measurement point. The given lists have to contain 3 components.
        /// </summary>
        /// <param name="measuredFrom">The point to calculate the distance from</param>
        /// <param name="p0">One of the candidate for the further point</param>
        /// <param name="p1">One of the candidate for the further point</param>
        /// <returns>The point which is further from the measuredFrom point</returns>
        private static List<double> FartherPoint(List<double> measuredFrom, List<double> p0, List<double> p1)
        {
            if (measuredFrom.Count != 3 || p0.Count != 3 || p1.Count != 3)
            {
                throw new ArgumentOutOfRangeException("The given coordinate does not contain exactly 3 components.");
            }

            var sd0 = Math.Pow(p0[0] - measuredFrom[0], 2) +
                      Math.Pow(p0[1] - measuredFrom[1], 2) +
                      Math.Pow(p0[2] - measuredFrom[2], 2);

            var sd1 = Math.Pow(p1[0] - measuredFrom[0], 2) +
                      Math.Pow(p1[1] - measuredFrom[1], 2) +
                      Math.Pow(p1[2] - measuredFrom[2], 2);

            return sd0 > sd1 ? p0 : p1;
        }

        /// <summary>
        /// Converts a Revit CurveArray to an IfcIndexedPolyCurve and applies the given Revit, Ifc, and unit conversions.
        /// </summary>
        /// <param name="curveArray">The CurveArray to convert into an IfcIndexedPolyCurve</param>
        /// <param name="closed">Whether to close the curve or not</param>
        /// <param name="model">The Ifc model to create the IfcIndexedPolyCurve in</param>
        /// <param name="revitTransform">Revit transformation matrix</param>
        /// <param name="ifcTransform">Ifc (Xbim) transformation matrix</param>
        /// <param name="unitConversion">Unit conversion multiplier</param>
        /// <returns>An IfcIndexedPolycurve with the applied transformations and unit conversion</returns>
        public static IfcIndexedPolyCurve ToIfcIndexedPolyCurve(this CurveArray curveArray, bool closed, IfcStore model,
                                                                Transform revitTransform, XbimMatrix3D ifcTransform, 
                                                                double unitConversion, bool is3D = true)
        {
            var segmentList = new List<Curve>();

            foreach (Curve curve in curveArray)
            {
                segmentList.Add(curve);
            }

            var segments = segmentList.ToArray();

            return segments.ToIfcIndexedPolyCurve(closed, model, revitTransform, ifcTransform, unitConversion, is3D);
        }

        /// <summary>
        /// Converts a Revit GeometryElement to an IfcIndexedPolyCurve and applies the given Revit, Ifc, and unit conversions.
        /// </summary>
        /// <param name="geometryElement">The GeometryElement to convert into an IfcIndexedPolyCurve</param>
        /// <param name="closed">Whether to close the curve or not</param>
        /// <param name="model">The Ifc model to create the IfcIndexedPolyCurve in</param>
        /// <param name="revitTransform">Revit transformation matrix</param>
        /// <param name="ifcTransform">Ifc (Xbim) transformation matrix</param>
        /// <param name="unitConversion">Unit conversion multiplier</param>
        /// <returns>An IfcIndexedPolycurve with the applied transformations and unit conversion</returns>
        public static IfcIndexedPolyCurve ToIfcIndexedPolyCurve(this GeometryElement geometryElement, bool closed, 
                                                                IfcStore model, Transform revitTransform,
                                                                XbimMatrix3D ifcTransform,  double unitConversion,
                                                                bool is3D = true)
        {
            var geometryObjectList = geometryElement.ToList();

            var segmentList = new List<Curve>();

            foreach (Curve curve in geometryObjectList)
            {
                segmentList.Add(curve);
            }

            var segments = segmentList.ToArray();

            return segments.ToIfcIndexedPolyCurve(closed, model, revitTransform, ifcTransform, unitConversion, is3D);
        }


        /// <summary>
        /// Converts a Revit GeometryElement to an IfcIndexedPolyCurve and applies the given Revit, Ifc, and unit conversions.
        /// </summary>
        /// <param name="segments">The Curve array to convert into an IfcIndexedPolyCurve</param>
        /// <param name="closed">Whether to close the curve or not</param>
        /// <param name="model">The Ifc model to create the IfcIndexedPolyCurve in</param>
        /// <param name="revitTransform">Revit transformation matrix</param>
        /// <param name="ifcTransform">Ifc (Xbim) transformation matrix</param>
        /// <param name="unitConversion">Unit conversion multiplier</param>
        /// <returns>An IfcIndexedPolycurve with the applied transformations and unit conversion</returns>
        private static IfcIndexedPolyCurve ToIfcIndexedPolyCurve(this Curve[] segments, bool closed, IfcStore model,
                                                                Transform revitTransform, XbimMatrix3D ifcTransform,
                                                                double unitConversion, bool is3D = true)
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
                    var cadPoint = revitTransform.OfPoint(firstSegment.GetEndPoint(0)) * unitConversion;

                    points.Add(new List<double>(){
                        cadPoint.X,
                        cadPoint.Y,
                        cadPoint.Z
                    });
                }
                else
                {
                    var cadPoint = revitTransform.OfPoint(firstSegment.GetEndPoint(1)) * unitConversion;

                    points.Add(new List<double>(){
                        cadPoint.X,
                        cadPoint.Y,
                        cadPoint.Z
                    });
                }

            }
            else
            {
                var cadPoint = revitTransform.OfPoint(firstSegment.GetEndPoint(0)) * unitConversion;

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
                        var arc = segment as Arc;


                        var tessellatedArc = arc.Tessellate();


                        var midPoint = arc.Evaluate(0.5f, true);

                        var cadPoint = revitTransform.OfPoint(midPoint) * unitConversion;
                        var transMidPoint = cadPoint;

                        points.Add(new List<double>(){
                            cadPoint.X,
                            cadPoint.Y,
                            cadPoint.Z
                        });

                        segmentIndices.Add(points.Count);

                        cadPoint = revitTransform.OfPoint(segment.GetEndPoint(0)) * unitConversion;
                        var transStartPoint = cadPoint;


                        var p0 = new List<double>(){
                            cadPoint.X,
                            cadPoint.Y,
                            cadPoint.Z
                        };


                        cadPoint = revitTransform.OfPoint(segment.GetEndPoint(1)) * unitConversion;
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
                        var cadPoint = revitTransform.OfPoint(line.GetEndPoint(1)) * unitConversion;

                        points.Add(new List<double>(){
                            cadPoint.X,
                            cadPoint.Y,
                            cadPoint.Z
                        });
                    }

                    segmentIndices.Add(points.Count);
                    indices.Add(segmentIndices);

                }

                if (is3D)
                {
                    ifcCurve.Points = model.Instances.New<IfcCartesianPointList3D>(coordinates =>
                    {
                        for (int j = 0; j < points.Count; j++)
                        {
                            XbimPoint3D originalPoint = new XbimPoint3D(points[j][0], points[j][1], points[j][2]);
                            var transformedPoint = ifcTransform.Transform(originalPoint);

                            coordinates.CoordList.GetAt(j).AddRange(new IfcLengthMeasure[] { transformedPoint.X,
                                                                                         transformedPoint.Y,
                                                                                         transformedPoint.Z});
                        };
                    });
                }
                else
                {
                    ifcCurve.Points = model.Instances.New<IfcCartesianPointList2D>(coordinates =>
                    {
                        for (int j = 0; j < points.Count; j++)
                        {
                            XbimPoint3D originalPoint = new XbimPoint3D(points[j][0], points[j][1], points[j][2]);
                            var transformedPoint = ifcTransform.Transform(originalPoint);

                            coordinates.CoordList.GetAt(j).AddRange(new IfcLengthMeasure[] { transformedPoint.X,
                                                                                         transformedPoint.Y});
                        };
                    });
                }
                


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
