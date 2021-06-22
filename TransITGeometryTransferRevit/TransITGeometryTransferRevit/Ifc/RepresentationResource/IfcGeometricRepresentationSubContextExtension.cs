using Serilog;
using Xbim.Ifc;
using Xbim.Ifc4.Interfaces;
using Xbim.Ifc4.RepresentationResource;


namespace TransITGeometryTransferRevit.Ifc.RepresentationResource
{
    public static class IfcGeometricRepresentationSubContextExtension
    {

        public static IfcGeometricRepresentationSubContext MakeSimple(IfcStore model, IfcGeometricRepresentationContext parentRep = null, string contextIdentifier = null, double targetScale = 0.01)
        {
            if (parentRep == null)
            {
                foreach (IfcGeometricRepresentationContext rep in model.Instances.OfType<IfcGeometricRepresentationContext>())
                {
                    if (rep is IfcGeometricRepresentationSubContext)
                    {
                        continue;
                    }

                    if (rep.ContextType != null && rep.ContextType.Value == "Model")
                    {
                        parentRep = rep;
                        break;
                    }
                }
            }

            if (parentRep != null)
            {
                Log.Debug($"Creating {contextIdentifier} IfcGeometricRepresentationSubContext based on the {parentRep} representation");
                var subRep = model.Instances.New<IfcGeometricRepresentationSubContext>(sc =>
                {
                        // TODO: Fix commented values

                        sc.ContextIdentifier = contextIdentifier;
                    sc.ContextType = parentRep.ContextType;
                        // sc.CoordinateSpaceDimension = modelRep.CoordinateSpaceDimension;
                        sc.ParentContext = parentRep;
                        // sc.Precision = modelRep.Precision;
                        sc.TargetScale = targetScale;
                    sc.TargetView = IfcGeometricProjectionEnum.MODEL_VIEW;
                        // sc.TrueNorth = modelRep.TrueNorth;
                        // sc.UserDefinedTargetView = modelRep.UserDefinedTargetView;
                        // sc.WorldCoordinateSystem = modelRep.WorldCoordinateSystem;
                    });


                return subRep;
            }
            else
            {
                throw new System.ArgumentNullException("parentRep",
                                        "Parent IfcGeometricRepresentationContext representation is null.");
            }

        }

    }
}