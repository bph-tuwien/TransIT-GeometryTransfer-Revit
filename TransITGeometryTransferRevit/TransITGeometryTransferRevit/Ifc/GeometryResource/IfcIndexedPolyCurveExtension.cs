using System;
using System.Collections.Generic;
using System.Linq;

using Autodesk.Revit.DB;

using Xbim.Common;
using Xbim.Ifc4.GeometricModelResource;
using Xbim.Ifc4.GeometryResource;
using Xbim.Ifc4.MeasureResource;


namespace TransITGeometryTransferRevit.Ifc.GeometryResource
{
    /// <summary>
    /// Extension methods to convert various IfcIndexedPolyCurve entities to Revit ones.
    /// </summary>
    public static class IfcIndexedPolyCurveExtension
    {
        /// <summary>
        /// Recreates an IfcIndexedPolyCurve as a Revit (NurbSpline) Curve.
        /// </summary>
        /// <param name="ifcIndexedPolyCurve">The given curve to convert</param>
        /// <returns>Returns a new NurbSpline Curve based on the poly curve</returns>
        public static Curve ToCurve(this IfcIndexedPolyCurve ifcIndexedPolyCurve, XYZ offset = null)
        {
            if (offset == null)
            {
                offset = new XYZ(0, 0, 0);
            }

            List<XYZ> controlPoints = new List<XYZ>();
            List<double> weights = new List<double>();

            IfcCartesianPointList3D pointList = ifcIndexedPolyCurve.Points as IfcCartesianPointList3D;
            var coordList = pointList.CoordList;

            foreach (var coord in coordList)
            {
                controlPoints.Add(new XYZ(coord[0] + offset[0], coord[1] + offset[1], coord[2] + offset[2]));
                weights.Add(1.0);
            }

            Curve pathCurve = NurbSpline.CreateCurve(controlPoints, weights);

            return pathCurve;
        }

        /// <summary>
        /// Recreates an IfcIndexedPolyCurve as a Revit CurveLoop. If the given curve is not closed, the new CurveLoop
        /// will be closed with a simple Line segment between the end point and start point. Duplicated points at the
        /// end of the polycurve is removed. Invalid segments by the point removal is also removed.
        /// </summary>
        /// <param name="ifcIndexedPolyCurve">The given curve to convert</param>
        /// <returns>Returns a Closed CurveLoop based on the poly curve</returns>
        public static CurveLoop ToCurveLoop(this IfcIndexedPolyCurve ifcIndexedPolyCurve)
        {
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

            return profile;
        }

        /// <summary>
        /// Converts an Ifc coordinate to a Revit XYZ.
        /// </summary>
        /// <param name="coord">The 3 dimensional Ifc coordinate to convert</param>
        /// <returns>Returns the coordinate as a Revit XYZ coordinate</returns>
        private static XYZ ToXYZ(this IItemSet<IfcLengthMeasure> coord)
        {
            // TODO: Length check
            if (coord.Count != 3)
            {
                throw new ArgumentOutOfRangeException("The given coordinate does not contain exactly 3 components.");
            }
            return new XYZ(coord[0], coord[1], coord[2]);
        }

        /// <summary>
        /// Recreates an IfcSegmentIndexSelect and IItemSet<IItemSet<IfcLengthMeasure>> pair (Ifc way of defining
        /// segments) as a Revit Curve (either Line or Arc).
        /// </summary>
        /// <param name="segment">The given segment containing the indices</param>
        /// <param name="coordList">All the coordinates in the containing curve, not just the segment's coordinates</param>
        /// <returns>Returns the segment as a Revit Curve, either Line or Arc</returns>
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
