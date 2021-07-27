using System.Collections.Generic;
using System.IO;
using System.Linq;

using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.ApplicationServices;

using Xbim.Ifc;
using Xbim.Ifc.Extensions;
using Xbim.Ifc4.GeometryResource;
using Xbim.Ifc4.Kernel;
using Xbim.Ifc4.RepresentationResource;
using Xbim.Ifc4.ProductExtension;
using Xbim.Ifc4.GeometricModelResource;
using Xbim.Ifc4.GeometricConstraintResource;
using Xbim.Ifc4.SharedBldgElements;
using Xbim.Ifc4.ProfileResource;
using Xbim.Ifc4.PresentationAppearanceResource;
using Xbim.Ifc4.PresentationOrganizationResource;
using Xbim.Ifc4.UtilityResource;
using Xbim.Ifc4.PropertyResource;
using Xbim.Common.Geometry;
using Xbim.Common;

using TransITGeometryTransferRevit.Ifc.GeometryResource;
using TransITGeometryTransferRevit.Revit;
using TransITGeometryTransferRevit.Ifc;


namespace TransITGeometryTransferRevit.Commands
{
    /// <summary>
    /// The class containing the callable Revit commands.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class ExportTunnel : IExternalCommand
    {
        /// <summary>
        /// Returns the Model IfcGeometricRepresentationContext from the IFC Model.
        /// </summary>
        /// <param name="model">The Ifc model to look for the Model representation context in</param>
        /// <returns> Model IfcGeometricRepresentationContext</returns>
        public static IfcGeometricRepresentationContext GetModelRepresentationContext(IfcStore model)
        {
            foreach (IfcGeometricRepresentationContext rep in model.Instances.OfType<IfcGeometricRepresentationContext>())
            {
                if (rep is IfcGeometricRepresentationSubContext)
                {
                    continue;
                }

                if (rep.ContextType != null && rep.ContextType.Value == "Model")
                {
                    return rep;
                }
            }
            return null;
        }

        /// <summary>
        /// Exporting the given Revit document to IFC version 4 to the given folder and file name.
        /// </summary>
        /// <param name="doc">The Revit document to export</param>
        /// <param name="ifcExportPathFolder">Path to the output folder</param>
        /// <param name="ifcExportPathFilename">File name of the exported IFC model</param>
        public void ExportDocumentToIfc(Document doc, string ifcExportPathFolder, string ifcExportPathFilename)
        {
            Transaction transaction = new Transaction(doc);
            {
                transaction.Start("Exporting document to IFC");

                var ifcOptions = new IFCExportOptions
                {
                    FileVersion = IFCVersion.IFC4
                };

                doc.Export(ifcExportPathFolder, ifcExportPathFilename, ifcOptions);

                transaction.Commit();
            }
        }

        /// <summary>
        /// Bumping the given IFC4 file's version to IFC4X1 to get access to new IFC entities. The given IFC file will
        /// be replaced with the new one.
        /// </summary>
        /// <param name="ifcExportPath">The original IFC4 file</param>
        /// <param name="ifcExportTempPath">A filepath to use as temp for the file replace</param>
        public void BumpIFCVersionTo4X1(string ifcExportPath, string ifcExportTempPath)
        {
            // TODO: Find a better way of schema upgrade instead of string replace

            using (var input = File.OpenText(ifcExportPath))
            using (var output = new StreamWriter(ifcExportTempPath))
            {
                string line;
                while (null != (line = input.ReadLine()))
                {
                    if (line.Equals("FILE_SCHEMA(('IFC4'));"))
                    {
                        output.WriteLine("FILE_SCHEMA(('IFC4X1'));");
                    }
                    else
                    {
                        output.WriteLine(line);
                    }
                }
            }

            File.Replace(ifcExportTempPath, ifcExportPath, null);
        }

        /// <summary>
        /// Generic function to find elements by type and name.
        /// </summary>
        /// <typeparam name="T">The type on the elements to find</typeparam>
        /// <param name="doc">The Revit document to search in</param>
        /// <param name="startsWith">String to filter elements by String.StartsWith. Leave it empty for no filtering</param>
        /// <returns></returns>
        public T[] GetElements<T>(Document doc, string startsWith) where T : class
        {
            FilteredElementCollector collector = new FilteredElementCollector(doc);
            collector = collector.OfClass(typeof(T));

            var query = from element in collector where element.Name.StartsWith(startsWith) select element;
            List<Element> elements = query.ToList<Element>();

            List<T> result = new List<T>();

            foreach (var elem in elements)
            {
                result.Add(elem as T);
            }

            return result.ToArray();
        }

        /// <summary>
        /// Adding Tunnel Line as Axis representation to the Tunnel.
        /// </summary>
        /// <param name="doc">The Revit document that contains the Tunnel as a FamilyInstance</param>
        /// <param name="ifcFilePath">The IFC file's filepath to do the addition in</param>
        public void AddTunnelLineAsAxisRepresentation(Document doc, string ifcFilePath)
        {
            using (var model = IfcStore.Open(ifcFilePath))
            {

                using (var ifcTransaction = model.BeginTransaction(
                                        "TransITGeometryTransferRevit.Commands.Dev.AddTunnelLineAsAxisRepresentation"))
                {

                    FamilyInstance tunnelFamilyInstance = GetElements<FamilyInstance>(doc, "TunnelFamily").First();
                    var fam = tunnelFamilyInstance.Symbol.Family;
                    var docfamily = doc.EditFamily(fam);
                    var tunnelFamilyInstanceTotalTransform = tunnelFamilyInstance.GetTotalTransform();

                    DirectShape tunnelLineShape = GetElements<DirectShape>(docfamily, "TunnelLine").First();
                    GeometryElement tunnelLineGeometry = tunnelLineShape.get_Geometry(new Options());

                    var ifcIndexedPolyCurve = tunnelLineGeometry.ToIfcIndexedPolyCurve(false, model, tunnelFamilyInstanceTotalTransform,
                                                                                       XbimMatrix3D.Identity, Constants.FeetToMillimeter);

                    var tunnelBuilding = model.Instances.OfType<IfcBuilding>().First();

                    tunnelBuilding.Representation = model.Instances.New<IfcProductDefinitionShape>(def =>
                    {
                        def.Representations.Add(model.Instances.New<IfcShapeRepresentation>(rep =>
                        {
                            rep.ContextOfItems = GetModelRepresentationContext(model);
                            rep.RepresentationIdentifier = "Axis";
                            rep.RepresentationType = "Curve3D";
                            rep.Items.Add(ifcIndexedPolyCurve);

                        }
                        ));
                    });


                    ifcTransaction.Commit();
                }

                NameIfcApplicationAndOrganization(model, "AddTunnelLineAsAxisRepresentation");

                model.SaveAs(ifcFilePath);
            }
        }

        /// <summary>
        /// Removes leftover entities like IfcIndexedColourMaps and broken IfcPresentationLayerAssignments.
        /// </summary>
        /// <param name="ifcFilePath">The IFC file's filepath to do the removal in</param>
        public void DeleteLeftOverEntities(string ifcFilePath)
        {
            using (var model = IfcStore.Open(ifcFilePath))
            {
                var ifcBuildingElementProxies = model.Instances.OfType<IfcBuildingElementProxy>();
                List<IPersistEntity> entitiesToDelete = new List<IPersistEntity>();


                // Local function to delete entities from the entitiesToDelete List
                void DeleteEntities()
                {
                    while (entitiesToDelete.Count > 0)
                    {
                        var entity = entitiesToDelete.First();
                        entitiesToDelete.RemoveAt(0);

                        using (var ifcTransaction = model.BeginTransaction(
                                                    "TransITGeometryTransferRevit.Commands.Dev.DeleteTunnelSectionsInIFC"))
                        {
                            try
                            {
                                entity.Model.Delete(entity);
                            }
                            catch (System.Exception e)
                            {
                                // Hide exceptions about already deleted entities
                            }
                            ifcTransaction.Commit();
                        }
                    }
                }


                var ifcIndexedColourMaps = model.Instances.OfType<IfcIndexedColourMap>();

                foreach (var colourMap in ifcIndexedColourMaps)
                {
                    if (colourMap.MappedTo == null)
                    {
                        entitiesToDelete.Add(colourMap);
                    }
                }

                var ifcPresentationLayerAssignment = model.Instances.OfType<IfcPresentationLayerAssignment>();

                foreach (var rel in ifcPresentationLayerAssignment)
                {
                    if (rel.AssignedItems.Count == 0)
                    {
                        entitiesToDelete.Add(rel);
                    }
                }

                DeleteEntities();

                NameIfcApplicationAndOrganization(model, "DeleteTunnelSectionsInIFC");

                model.SaveAs(ifcFilePath);
            }
        }

        /// <summary>
        /// Converts a Revit Profile Family to IfcIndexedPolyCurve.
        /// </summary>
        /// <param name="tunnelProfileDocument">The Revit family document containing the Tunnel profile</param>
        /// <param name="model">The Ifc model to create the IfcIndexedPolyCurve in</param>
        /// <returns>The Profile as IfcIndexedPolyCurve</returns>
        public IfcIndexedPolyCurve RevitProfileDocumentToIfcIndexedPolyCurve(Document tunnelProfileDocument, IfcStore model)
        {

            FilteredElementCollector collectorRevitTunnelProfile = new FilteredElementCollector(tunnelProfileDocument);
            collectorRevitTunnelProfile = collectorRevitTunnelProfile.OfClass(typeof(CurveElement));

            CurveArray profileCurveArray = new CurveArray();

            foreach (ModelArc modelArc in collectorRevitTunnelProfile)
            {
                var geometryArc = modelArc.GeometryCurve;
                if (geometryArc is Curve curve)
                {
                    profileCurveArray.Append(curve);
                }

            }

            return profileCurveArray.ToIfcIndexedPolyCurve(true, model, Transform.Identity, XbimMatrix3D.Identity,
                                                           Constants.FeetToMillimeter, false);
        }

        /// <summary>
        /// Deletes the Tunnel Section IfcBuildingElementProxies' child entities but not the IfcBuildingElementProxies
        /// themselves.
        /// </summary>
        /// <param name="ifcFilePath">The IFC file's filepath to do the removal in</param>
        public void DeleteTunnelSectionPartsInIFC(string ifcFilePath)
        {
            using (var model = IfcStore.Open(ifcFilePath))
            {
                var ifcBuildingElementProxies = model.Instances.OfType<IfcBuildingElementProxy>();
                List<IPersistEntity> entitiesToDelete = new List<IPersistEntity>();


                // Local function to delete entities from the entitiesToDelete List
                void DeleteEntities()
                {
                    while (entitiesToDelete.Count > 0)
                    {
                        var entity = entitiesToDelete.First();
                        entitiesToDelete.RemoveAt(0);

                        using (var ifcTransaction = model.BeginTransaction(
                                                    "TransITGeometryTransferRevit.Commands.Dev.DeleteTunnelSectionPartsInIFC"))
                        {
                            try
                            {
                                entity.Model.Delete(entity);
                            }
                            catch (System.Exception e)
                            {
                                // Hide exceptions about already deleted entities
                            }
                            ifcTransaction.Commit();
                        }
                    }
                }


                foreach (var tunnelSection in ifcBuildingElementProxies)
                {
                    entitiesToDelete.Add(
                        (((tunnelSection.ObjectPlacement as IfcLocalPlacement).RelativePlacement) as IfcAxis2Placement3D)
                        .Location);
                    entitiesToDelete.Add(
                        (((tunnelSection.ObjectPlacement as IfcLocalPlacement).RelativePlacement) as IfcAxis2Placement3D)
                        .Axis);
                    entitiesToDelete.Add(
                        (((tunnelSection.ObjectPlacement as IfcLocalPlacement).RelativePlacement) as IfcAxis2Placement3D)
                        .RefDirection);
                    entitiesToDelete.Add((tunnelSection.ObjectPlacement as IfcLocalPlacement).RelativePlacement);
                    entitiesToDelete.Add(tunnelSection.ObjectPlacement);



                    entitiesToDelete.AddRange(((tunnelSection.Representation.Representations[0].Items[0] as IfcMappedItem)
                        .MappingSource.MappedRepresentation.Items[0] as IfcPolygonalFaceSet).Faces);
                    entitiesToDelete.Add(((tunnelSection.Representation.Representations[0].Items[0] as IfcMappedItem)
                        .MappingSource.MappedRepresentation.Items[0] as IfcPolygonalFaceSet).Coordinates);
                    entitiesToDelete.Add((tunnelSection.Representation.Representations[0].Items[0] as IfcMappedItem)
                        .MappingSource.MappedRepresentation.Items[0]);
                    entitiesToDelete.Add((tunnelSection.Representation.Representations[0].Items[0] as IfcMappedItem)
                        .MappingSource.MappedRepresentation);
                    entitiesToDelete.Add((tunnelSection.Representation.Representations[0].Items[0] as IfcMappedItem)
                        .MappingSource.MappingOrigin);
                    entitiesToDelete.Add((tunnelSection.Representation.Representations[0].Items[0] as IfcMappedItem)
                        .MappingSource);
                    entitiesToDelete.Add(tunnelSection.Representation.Representations[0].Items[0]);
                    entitiesToDelete.Add(tunnelSection.Representation.Representations[0]);
                    entitiesToDelete.Add(tunnelSection.Representation);
                    //entitiesToDelete.Add(tunnelSection);

                }


                DeleteEntities();

                // Second run of collecting entities to delete, but this time based on empty references. (this way it's
                // easier to find entities that were part of the tunnel sections)


                var ifcIndexedColourMaps = model.Instances.OfType<IfcIndexedColourMap>();

                foreach (var colourMap in ifcIndexedColourMaps)
                {
                    if (colourMap.MappedTo == null)
                    {
                        entitiesToDelete.Add(colourMap);
                    }
                }


                DeleteEntities();

                NameIfcApplicationAndOrganization(model, "DeleteTunnelSectionPartsInIFC");

                model.SaveAs(ifcFilePath);
            }
        }

        /// <summary>
        /// Recreating Tunnel Sections based on Revit FamilyInstances.
        /// </summary>
        /// <param name="ifcFilePath">The IFC file's filepath to do the addition in</param>
        /// <param name="tunnelFamilyDocument">Tunnel Family Document containing the tunnel</param>
        public void RecreateTunnelSectionsInIFC(string ifcFilePath, Document tunnelFamilyDocument)
        {
            using (var model = IfcStore.Open(ifcFilePath))
            {
                using (var ifcTransaction = model.BeginTransaction(
                                            "TransITGeometryTransferRevit.Commands.Dev.RecreatingTunnelSectionsInIFC"))
                {

                    var revitTunnelSections = GetElements<FamilyInstance>(tunnelFamilyDocument, "TunnelSection");

                    var oldIfcBuildingElementProxies = model.Instances.OfType<IfcBuildingElementProxy>();


                    var ifcBuildingElementProxies = new List<IfcBuildingElementProxy>();


                    foreach (var revitTunnelSection in revitTunnelSections)
                    {
                        var placementPointIds = AdaptiveComponentInstanceUtils.GetInstancePlacementPointElementRefIds(revitTunnelSection);

                        Parameter sectionIDParam = revitTunnelSection.LookupParameter("SectionID");


                        // ##############################
                        // Finding the old Tunnel Section
                        // ##############################

                        IfcBuildingElementProxy oldIfcBuildingElementProxy = null;

                        foreach (var proxy in oldIfcBuildingElementProxies)
                        {
                            foreach (var propertySet in proxy.PropertySets)
                            {
                                if (propertySet.Name == "Data")
                                {
                                    foreach (var property in propertySet.HasProperties)
                                    {
                                        if (property.Name == "SectionID" && (property as IfcPropertySingleValue).NominalValue.ToString() == sectionIDParam.AsInteger().ToString())
                                        {
                                            oldIfcBuildingElementProxy = proxy;
                                        }
                                    }
                                }
                            }
                        }


                        var buildingElementProxy = model.Instances.New<IfcBuildingElementProxy>(b =>
                        {
                            
                            b.GlobalId = oldIfcBuildingElementProxy.GlobalId;
                            //b.OwnerHistory = 
                            b.Name = revitTunnelSection.Symbol.FamilyName + ":" +
                                         revitTunnelSection.Symbol.Name + ":" +
                                         sectionIDParam.AsInteger();
                            b.Description = oldIfcBuildingElementProxy.Description;
                            b.ObjectType = revitTunnelSection.Symbol.FamilyName + ":" +
                                               revitTunnelSection.Symbol.Name;
                            b.ObjectPlacement = model.Instances.New<IfcLocalPlacement>(p =>
                            {
                                var tunnelStorey = model.Instances.OfType<IfcBuildingStorey>().First();

                                p.PlacementRelTo = tunnelStorey.ObjectPlacement;
                                p.RelativePlacement = model.Instances.New<IfcAxis2Placement3D>(t =>
                                {

                                    t.Location = model.Instances.New<IfcCartesianPoint>(cp =>
                                    {
                                        var originPoint = tunnelFamilyDocument.GetElement(placementPointIds[0]) as ReferencePoint;

                                        cp.X = originPoint.Position.X * Constants.FeetToMillimeter;
                                        cp.Y = originPoint.Position.Y * Constants.FeetToMillimeter;
                                        cp.Z = originPoint.Position.Z * Constants.FeetToMillimeter;
                                    });

                                    t.Axis = model.Instances.New<IfcDirection>(d =>
                                    {
                                        d.X = 0;
                                        d.Y = 0;
                                        d.Z = 1;
                                    });

                                    t.RefDirection = model.Instances.New<IfcDirection>(d =>
                                    {
                                        d.X = 1;
                                        d.Y = 0;
                                        d.Z = 0;
                                    });
                                });
                            });

                            var p0 = tunnelFamilyDocument.GetElement(placementPointIds[0]) as ReferencePoint;
                            var p1 = tunnelFamilyDocument.GetElement(placementPointIds[1]) as ReferencePoint;

                            var xyz0 = new XYZ(0, 0, 0);
                            var xyz1 = p1.Position - p0.Position;

                            var revitTunnelSectionLine = Line.CreateBound(xyz0, xyz1);
                            var revitTunnelSectionLineCurveArray = new CurveArray();
                            revitTunnelSectionLineCurveArray.Append(revitTunnelSectionLine);

                            var ifcTunnelSectionLine = revitTunnelSectionLineCurveArray.ToIfcIndexedPolyCurve(false,
                                        model, Transform.Identity, XbimMatrix3D.Identity, Constants.FeetToMillimeter);


                            var revitTunnelParts = new List<FamilyInstance>();


                            var revitTunnelProfiles = GetElements<FamilyInstance>(tunnelFamilyDocument, "");


                            foreach (FamilyInstance revitTunnelProfile in revitTunnelProfiles)
                            {

                                if (revitTunnelProfile.SuperComponent != null &&
                                    revitTunnelSection.Id.ToString() == revitTunnelProfile.SuperComponent.Id.ToString())
                                {
                                    revitTunnelParts.Add(revitTunnelProfile);
                                }
                            }


                            b.Representation = model.Instances.New<IfcProductDefinitionShape>(def =>
                            {

                                var sweep = model.Instances.New<IfcSectionedSolidHorizontal>(s =>
                                {
                                    s.Directrix = ifcTunnelSectionLine;

                                    var revitTunnelProfileFamily1 = revitTunnelParts[0].Symbol.Family;
                                    var revitTunnelProfileDocument1 = tunnelFamilyDocument.EditFamily(revitTunnelProfileFamily1);
                                    var profileIfcIndexedPolyCurve1 = RevitProfileDocumentToIfcIndexedPolyCurve(revitTunnelProfileDocument1, model);

                                    var revitTunnelProfileFamily2 = revitTunnelParts[1].Symbol.Family;
                                    var revitTunnelProfileDocument2 = tunnelFamilyDocument.EditFamily(revitTunnelProfileFamily2);
                                    var profileIfcIndexedPolyCurve2 = RevitProfileDocumentToIfcIndexedPolyCurve(revitTunnelProfileDocument2, model);


                                    s.CrossSections.Add(model.Instances.New<IfcArbitraryClosedProfileDef>(p =>
                                    {
                                        p.ProfileType = Xbim.Ifc4.Interfaces.IfcProfileTypeEnum.AREA;
                                        p.ProfileName = "Profile1";
                                        p.OuterCurve = profileIfcIndexedPolyCurve1;
                                    }
                                    ));
                                    s.CrossSections.Add(model.Instances.New<IfcArbitraryClosedProfileDef>(p =>
                                    {
                                        p.ProfileType = Xbim.Ifc4.Interfaces.IfcProfileTypeEnum.AREA;
                                        p.ProfileName = "Profile2";
                                        p.OuterCurve = profileIfcIndexedPolyCurve2;
                                    }
                                    ));
                                    s.CrossSectionPositions.Add(model.Instances.New<IfcDistanceExpression>(d =>
                                    {
                                        d.DistanceAlong = 0;
                                        //d.OffsetLateral = 0;
                                        //d.OffsetVertical = 0;
                                        d.AlongHorizontal = false;
                                    }
                                    ));

                                    s.CrossSectionPositions.Add(model.Instances.New<IfcDistanceExpression>(d =>
                                    {
                                        // Calculating line length
                                        IfcCartesianPointList3D pointList = ifcTunnelSectionLine.Points as IfcCartesianPointList3D;
                                        var coordList = pointList.CoordList;
                                        var x0 = new XYZ(coordList[0][0], coordList[0][1], coordList[0][2]);
                                        var x1 = new XYZ(coordList[1][0], coordList[1][1], coordList[1][2]);
                                        // Decrement the length of the tunnel by a small amount so the sweep shows up
                                        // Could be a FZK Viewer specific issue and solution
                                        var length = (x1 - x0).GetLength() - 0.00000000001;


                                        d.DistanceAlong = length;
                                        //d.OffsetLateral = 0;
                                        //d.OffsetVertical = 0;
                                        d.AlongHorizontal = false;
                                    }
                                    ));

                                    s.FixedAxisVertical = false;
                                });


                                def.Representations.Add(model.Instances.New<IfcShapeRepresentation>(rep =>
                                {

                                    rep.ContextOfItems = GetModelRepresentationContext(model);
                                    rep.RepresentationIdentifier = "Body";
                                    rep.RepresentationType = "AdvancedSweptSolid";
                                    rep.Items.Add(sweep);

                                }));

                            });

                            b.Tag = oldIfcBuildingElementProxy.Tag;
                            b.PredefinedType = oldIfcBuildingElementProxy.PredefinedType;

                        });

                        ReferenceUtils.RedirectAllReferenesInModel(model, oldIfcBuildingElementProxy, buildingElementProxy);
                        oldIfcBuildingElementProxy.Model.Delete(oldIfcBuildingElementProxy);


                    }

                    ifcTransaction.Commit();

                }

                NameIfcApplicationAndOrganization(model, "RecreateTunnelSectionsInIFC");

                model.SaveAs(ifcFilePath);
            }
        }

        /// <summary>
        /// Adds Tunnel Section Profiles as Profile rpresentation to the Tunnel Sections.
        /// </summary>
        /// <param name="doc">The Revit Document containing the Tunnel FamilyInstance</param>
        /// <param name="ifcFilePath">The IFC file's filepath to do the addition in</param>
        public void AddTunnelSectionProfilesAsProfileRepresentation(Document doc, string ifcFilePath)
        {
            using (var model = IfcStore.Open(ifcFilePath))
            {
                var revitTunnelSections = GetElements<FamilyInstance>(doc, "TunnelSection");

                using (var ifcTransaction = model.BeginTransaction(
                            "TransITGeometryTransferRevit.Commands.Dev.AddTunnelSectionProfilesAsProfileRepresentation"))
                {
                    var ifcTunnelSections = model.Instances.OfType<IfcBuildingElementProxy>();

                    foreach (var revitTunnelSection in revitTunnelSections)
                    {

                        var revitTunnelSectionFamilyInstance = revitTunnelSection as FamilyInstance;

                        foreach (var ifcTunnelSection in ifcTunnelSections)
                        {

                            // Matching Revit and IFC tunnel sections
                            Parameter sectionIDParam = revitTunnelSectionFamilyInstance.LookupParameter("SectionID");

                            var ifcTunnelSectionID = ifcTunnelSection.Name.ToString().Split(':')[2];

                            if (ifcTunnelSectionID == sectionIDParam.AsInteger().ToString())
                            {

                                var allFamilyInstances = GetElements<FamilyInstance>(doc, "");

                                var ifcTunnelSectionProfiles = new List<IfcIndexedPolyCurve>();

                                var ifcTransform = ifcTunnelSection.ObjectPlacement.ToMatrix3D();
                                ifcTransform.Invert();

                                // Finding the parts of the Tunnel Section
                                foreach (FamilyInstance instance in allFamilyInstances)
                                {
                                    if (instance.SuperComponent != null &&
                                        revitTunnelSection.Id.ToString() == instance.SuperComponent.Id.ToString())
                                    {
                                        var revitTunnelSectionProfile = instance;


                                        Options options = new Options();
                                        options.ComputeReferences = true;
                                        options.DetailLevel = ViewDetailLevel.Undefined;
                                        options.IncludeNonVisibleObjects = false;

                                        GeometryElement geoElement = revitTunnelSectionProfile.get_Geometry(options);
                                        GeometryInstance geoInstance = geoElement.First() as GeometryInstance;
                                        GeometryElement instanceGeometry = geoInstance.GetInstanceGeometry();

                                        CurveArray profileCurveArray = new CurveArray();

                                        foreach (var part in instanceGeometry)
                                        {
                                            if (part is Curve curve)
                                            {
                                                profileCurveArray.Append(curve);
                                            }
                                        }

                                        var profileIfcIndexedPolyCurve = profileCurveArray.ToIfcIndexedPolyCurve(true,
                                                    model, Transform.Identity, ifcTransform, Constants.FeetToMillimeter);

                                        ifcTunnelSectionProfiles.Add(profileIfcIndexedPolyCurve);

                                    }
                                }


                                ifcTunnelSection.Representation.Representations.Add(model.Instances.New<IfcShapeRepresentation>(rep =>
                                {
                                    rep.ContextOfItems = GetModelRepresentationContext(model);
                                    rep.RepresentationIdentifier = "Profile";
                                    rep.RepresentationType = "Curve3D";
                                    rep.Items.AddRange(ifcTunnelSectionProfiles);
                                }
                                ));
                            }
                        }
                    }

                    ifcTransaction.Commit();
                }

                model.SaveAs(ifcFilePath);
            }
        }

        /// <summary>
        /// Adds Tunnel Section Lines as Axis rpresentation to the Tunnel Sections.
        /// </summary>
        /// <param name="doc">The Revit Document containing the Tunnel FamilyInstance</param>
        /// <param name="ifcFilePath">The IFC file's filepath to do the addition in</param>
        public void AddTunnelSectionLinesAsAxisRepresentation(Document doc, string ifcFilePath)
        {
            using (var model = IfcStore.Open(ifcFilePath))
            {
                var revitTunnelSections = GetElements<FamilyInstance>(doc, "TunnelSection");


                using (var ifcTransaction = model.BeginTransaction(
                                "TransITGeometryTransferRevit.Commands.Dev.AddTunnelSectionLinesAsAxisRepresentation"))
                {
                    var ifcTunnelSections = model.Instances.OfType<IfcBuildingElementProxy>();


                    foreach (var revitTunnelSection in revitTunnelSections)
                    {

                        var revitTunnelSectionFamilyInstance = revitTunnelSection as FamilyInstance;

                        foreach (var ifcTunnelSection in ifcTunnelSections)
                        {

                            // Matching Revit and IFC tunnel sections
                            Parameter sectionIDParam = revitTunnelSectionFamilyInstance.LookupParameter("SectionID");

                            var ifcTunnelSectionID = ifcTunnelSection.Name.ToString().Split(':')[2];

                            if (ifcTunnelSectionID == sectionIDParam.AsInteger().ToString())
                            {
                                var allFamilyInstances = GetElements<FamilyInstance>(doc, "");


                                var revitTunnelSectionprofiles = new List<FamilyInstance>();

                                var ifcTransform = ifcTunnelSection.ObjectPlacement.ToMatrix3D();
                                ifcTransform.Invert();

                                // Finding the parts of the Tunnel Section
                                foreach (FamilyInstance instance in allFamilyInstances)
                                {
                                    if (instance.SuperComponent != null &&
                                        revitTunnelSection.Id.ToString() == instance.SuperComponent.Id.ToString())
                                    {
                                        revitTunnelSectionprofiles.Add(instance);

                                    }
                                }


                                var p0 = revitTunnelSectionprofiles[0].Location as LocationPoint;
                                var p1 = revitTunnelSectionprofiles[1].Location as LocationPoint;

                                var revitTunnelSectionLine = Line.CreateBound(p0.Point, p1.Point);
                                var revitTunnelSectionLineCurveArray = new CurveArray();
                                revitTunnelSectionLineCurveArray.Append(revitTunnelSectionLine);

                                var ifcTunnelSectionLine = revitTunnelSectionLineCurveArray.ToIfcIndexedPolyCurve(false,
                                                    model, Transform.Identity, ifcTransform, Constants.FeetToMillimeter);

                                ifcTunnelSection.Representation.Representations.Add(model.Instances.New<IfcShapeRepresentation>(rep =>
                                {
                                    rep.ContextOfItems = GetModelRepresentationContext(model);
                                    rep.RepresentationIdentifier = "Axis";
                                    rep.RepresentationType = "Curve3D";
                                    rep.Items.Add(ifcTunnelSectionLine);
                                }
                                ));

                            }
                        }
                    }

                    ifcTransaction.Commit();
                }

                model.SaveAs(ifcFilePath);
            }
        }


        /// <summary>
        /// Naming Unspecified IfcApplication and IfcOrganization entities based on the given export stage string.
        /// </summary>
        /// <param name="model">The Ifc model to make the modifications in</param>
        /// <param name="exportStage">Name of the last export stage</param>
        public void NameIfcApplicationAndOrganization(IfcStore model, string exportStage)
        {
            using (var ifcTransaction = model.BeginTransaction(
                           "TransITGeometryTransferRevit.Commands.Dev.NameIfcApplicationAndOrganization"))
            {

                var ifcApplications = model.Instances.OfType<IfcApplication>();

                foreach (var ifcApplication in ifcApplications)
                {
                    if (ifcApplication.ApplicationFullName != "Unspecified")
                    {
                        continue;
                    }

                    ifcApplication.ApplicationDeveloper.Name = $"TransIT-GeometryTransfer-Revit {exportStage}";
                    ifcApplication.Version = "1.1.0";
                    ifcApplication.ApplicationFullName = $"TransIT-GeometryTransfer-Revit {exportStage}";
                    ifcApplication.ApplicationIdentifier = $"TransIT {exportStage}";
                }


                ifcTransaction.Commit();
            }
        }

        /// <summary>
        /// Dev command class
        /// </summary>
        /// <param name="commandData"></param>
        /// <param name="message"></param>
        /// <param name="elements"></param>
        /// <returns></returns>
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Application app = uiapp.Application;
            Document doc = uidoc.Document;


            FamilyInstance tunnelFamilyInstance = GetElements<FamilyInstance>(doc, "TunnelFamily").First();
            var fam = tunnelFamilyInstance.Symbol.Family;
            var tunnelFamilyDocument = doc.EditFamily(fam);


            var ifcExportFullPath = UserInteractions.PromptIfcFileSaveDialog();

            var ifcExportPathFolder = Path.GetDirectoryName(ifcExportFullPath);
            var ifcExportPathFilename = Path.GetFileName(ifcExportFullPath);
            var ifcExportPath = Path.Combine(ifcExportPathFolder, ifcExportPathFilename);
            var ifcExportTempPath = Path.Combine(ifcExportPathFolder, ifcExportPathFilename + "_temp");


            ExportDocumentToIfc(doc, ifcExportPathFolder, ifcExportPathFilename);
            BumpIFCVersionTo4X1(ifcExportPath, ifcExportTempPath);
            AddTunnelLineAsAxisRepresentation(doc, ifcExportPath);
            DeleteTunnelSectionPartsInIFC(ifcExportPath);
            RecreateTunnelSectionsInIFC(ifcExportPath, tunnelFamilyDocument);
            DeleteLeftOverEntities(ifcExportPath);
            AddTunnelSectionProfilesAsProfileRepresentation(doc, ifcExportPath);
            AddTunnelSectionLinesAsAxisRepresentation(doc, ifcExportPath);


            return Result.Succeeded;
        }
    }
}
