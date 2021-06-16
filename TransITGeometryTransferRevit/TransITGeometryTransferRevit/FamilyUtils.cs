using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB.Structure;

using Xbim.Ifc;
using Xbim.Ifc4.GeometryResource;
using Xbim.Ifc4.Kernel;
using Xbim.Ifc4.RepresentationResource;
using Xbim.Ifc4.GeometricModelResource;

namespace TransITGeometryTransferRevit
{
    class FamilyUtils
    {

        /// <summary>
        /// Loads the Revit Family if it's not loaded already.
        /// </summary>
        /// <param name="doc">The Revit document to load the family into</param>
        /// <param name="familyPath">Absolute file path of the family</param>
        /// <param name="familyName">The name that will be used to name the loaded family</param>
        /// <returns>The loaded family</returns>
        public static Family LoadFamilyIfNotLoaded(Document doc, string familyPath, string familyName)
        {
            Family family = null;

            FilteredElementCollector a = new FilteredElementCollector(doc).OfClass(typeof(Family));

            int n = a.Count<Element>(e => e.Name.Equals(familyName));

            if (0 < n)
            {
                family = a.First<Element>(e => e.Name.Equals(familyName)) as Family;
            }
            else
            {
                doc.LoadFamily(familyPath, out family);
            }

            return family;
        }

    }
}
