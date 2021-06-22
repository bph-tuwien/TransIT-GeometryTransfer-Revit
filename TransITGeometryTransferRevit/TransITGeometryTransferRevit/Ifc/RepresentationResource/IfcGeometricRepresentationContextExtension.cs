using Xbim.Ifc;
using Xbim.Ifc4.GeometryResource;
using Xbim.Ifc4.RepresentationResource;

namespace TransITGeometryTransferRevit.Ifc.RepresentationResource
{
    public static class IfcGeometricRepresentationContextExtension
    {

        public static IfcGeometricRepresentationContext MakeSimple(IfcStore model, string contextType = null,
            string contextIdentifier = null, double precision = 1e-5)
        {
            var trueNorthDirection = model.Instances.New<IfcDirection>(dir =>
            {
                dir.X = 0;
                dir.Y = 1;
                dir.Z = 0;
            });

            var origin = model.Instances.New<IfcCartesianPoint>(p =>
            {
                p.X = 0;
                p.Y = 0;
                p.Z = 0;
            });

            var axis = model.Instances.New<IfcDirection>(dir =>
            {
                dir.X = 0;
                dir.Y = 0;
                dir.Z = 1;
            });
            var refDirection = model.Instances.New<IfcDirection>(dir =>
            {
                dir.X = 1;
                dir.Y = 0;
                dir.Z = 0;
            });

            var worldCoordinateSystem = model.Instances.New<IfcAxis2Placement3D>(w =>
            {
                w.Location = origin;
                w.Axis = axis;
                w.RefDirection = refDirection;
            });

            var modelRep = model.Instances.New<IfcGeometricRepresentationContext>(sc =>
            {
                sc.ContextIdentifier = contextIdentifier;
                sc.ContextType = contextType;
                sc.CoordinateSpaceDimension = 3;
                sc.Precision = precision;
                    //sc.TrueNorth = trueNorthDirection;
                    sc.WorldCoordinateSystem = worldCoordinateSystem;
            });


            return modelRep;

        }

    }
}