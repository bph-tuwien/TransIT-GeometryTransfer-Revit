using System.Collections.Generic;
using System.Collections;
using System.Reflection;
using System.Linq;

using Xbim.Ifc;
using Xbim.Ifc4.SharedBldgElements;
using Xbim.Ifc4.ProductExtension;


namespace TransITGeometryTransferRevit.Ifc
{
    /// <summary>
    /// The class that handles entity reference redirections in IFC models.
    /// </summary>
    public class ReferenceUtils
    {
        /// <summary>
        /// Redirects all references of the oldReferencedEntity to the newReferencedEntity. 
        /// If a list of newReferencedEntity is received, it sets the oldReferenceEntity´s references to all elment in 
        /// the list. 
        /// </summary>
        /// <param name="model">The Ifc model to do the reference redirections</param>
        /// <param name="oldReferencedEntity">The old IFC entity to redirect from</param>
        /// <param name="newReferencedEntity">The new IFC entity to redirect to</param>
        public static void RedirectAllReferenesInModel(IfcStore model, object oldReferencedEntity, object newReferencedEntity)
        {
            foreach (var entity in model.Instances)
            {

                ReferenceUtils.RedirectEntityReferences(model, entity, oldReferencedEntity, newReferencedEntity);
            }
        }

        /// <summary>
        /// Redirects a single reference of the referencingEntity to point to the newReferencedEntity instead of the 
        /// oldReferencedEntity.
        /// </summary>
        /// <param name="model">The Ifc model to do the reference redirection</param>
        /// <param name="referencingEntity">The entity that has the reference to the oldReferencedEntity</param>
        /// <param name="oldReferencedEntity">The old IFC entity to redirect from</param>
        /// <param name="newReferencedEntity">The new IFC entity to redirect to</param>
        public static void RedirectEntityReferences(IfcStore model, object referencingEntity, object oldReferencedEntity,
                                                    object newReferencedEntity)
        {


            ///////////////// CASE SPECIFIC ///////////////////////////////////////////////
            if (referencingEntity is IfcRelConnectsPathElements relConnectsPathElements)
            {
                RedirectRelConnectPathElement(model, relConnectsPathElements, oldReferencedEntity, newReferencedEntity);
            }
            if (referencingEntity is IfcRelAssociatesMaterial ifcRelAssMaterial)
            {
                RedirectRelAssociatesMaterial(model, ifcRelAssMaterial, oldReferencedEntity, newReferencedEntity);
            }



            foreach (PropertyInfo property in referencingEntity.GetType().GetProperties(
                                                            BindingFlags.Public |
                                                            BindingFlags.Instance)
                                                            .Where(p => !p.GetIndexParameters().Any()))
            {
                // TODO: Hiding random errors is not nice, I did get weird reflection exceptions but it seems functional
                try
                {
                    var value = property.GetValue(referencingEntity);
                    if (value != null)
                    {
                        // Checking if property value is a collection or single value
                        if (value.GetType().GetInterface(nameof(IList<object>)) == null)
                        {
                            // Single value type

                            if (newReferencedEntity is ICollection newReferencedEntities)
                            {
                                // Log.Warning("Cannot replace a single value reference with a collection reference");

                            }
                            else
                            {
                                if (value == oldReferencedEntity)
                                {
                                    property.SetValue(referencingEntity, newReferencedEntity);
                                }
                            }
                        }
                        else
                        {
                            // Collection value type
                            IList list = value as IList;
                            if (list != null)
                            {
                                List<object> toRemove = new List<object>();
                                List<object> toAdd = new List<object>();
                                foreach (var item in list)
                                {
                                    if (item == oldReferencedEntity)
                                    {
                                        if (newReferencedEntity is ICollection newReferencedEntities)
                                        {

                                            foreach (var nRefEntity in newReferencedEntities)
                                            {
                                                // list.Add(nRefEntity);
                                                toAdd.Add(nRefEntity);
                                            }
                                        }
                                        else
                                        {
                                            // list.Add(newReferencedEntity);
                                            toAdd.Add(newReferencedEntity);
                                        }

                                        toRemove.Add(item);
                                        ;
                                    }
                                }

                                foreach (var item in toRemove)
                                {
                                    list.Remove(item);
                                }

                                foreach (var item in toAdd)
                                {
                                    list.Add(item);
                                }

                            }
                        }
                    }
                }
                catch (System.Exception e)
                {
                    // Skipping TargetInvocationExceptions because of bunch of "The method or operation is not implemented."
                    // exceptions in the Xbim.Ifc4 lib
                    if (!(e is System.Reflection.TargetInvocationException))
                    {
                    }

                }

            }

        }



        private static void RedirectRelAssociatesMaterial(IfcStore model, IfcRelAssociatesMaterial relAssociatesMaterial, object oldReferencedEntity,
                                                    object newReferencedEntity)
        {
            if (relAssociatesMaterial.RelatedObjects.Contains(oldReferencedEntity))
            {
                if (newReferencedEntity is ICollection newReferencedEntities)
                {

                    foreach (var item in newReferencedEntities)
                    {
                        var new_rel = model.Instances.New<IfcRelAssociatesMaterial>(p =>
                        {
                            p.RelatingMaterial = relAssociatesMaterial.RelatingMaterial;
                            p.RelatedObjects.Add(newReferencedEntity as IfcElement);
                            p.Name = relAssociatesMaterial.Name;
                            p.Description = relAssociatesMaterial.Description;
                        });
                    }

                }
                else
                {

                }
            }
        }

        private static void RedirectRelConnectPathElement(IfcStore model, IfcRelConnectsPathElements relConnectsPathElements, object oldReferencedEntity,
                                                    object newReferencedEntity)
        {
            if (relConnectsPathElements.RelatingElement == oldReferencedEntity)
            {
                if (newReferencedEntity is ICollection newReferencedEntities)
                {

                    foreach (var item in newReferencedEntities)
                    {
                        var new_rel = model.Instances.New<IfcRelConnectsPathElements>(p =>
                        {
                            p.RelatedConnectionType = relConnectsPathElements.RelatedConnectionType;
                            p.Name = relConnectsPathElements.Name;
                            p.Description = relConnectsPathElements.Description;
                            p.RelatingConnectionType = relConnectsPathElements.RelatingConnectionType;
                            p.RelatedElement = relConnectsPathElements.RelatedElement;
                            p.RelatingElement = item as IfcElement;
                        });
                    }

                }
            }
            model.Delete(relConnectsPathElements);
        }
    }
}
