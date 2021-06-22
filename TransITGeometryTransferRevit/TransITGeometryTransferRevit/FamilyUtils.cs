using System.Collections.Generic;
using System.Linq;

using Autodesk.Revit.DB;


namespace TransITGeometryTransferRevit
{
    /// <summary>
    /// Helper class containing helper functions for handling Revit families.
    /// </summary>
    public class FamilyUtils
    {
        public static Document GetFamilyDocumentByName(Document doc, string familyName)
        {
            FilteredElementCollector collector = new FilteredElementCollector(doc);
            collector = collector.OfClass(typeof(FamilyInstance));

            var query = from element in collector where element.Name.Equals(familyName) select element;
            List<Element> result = query.ToList<Element>();


            FamilyInstance tunnelFamilyInstance = query.First() as FamilyInstance;
            var fam = tunnelFamilyInstance.Symbol.Family;
            var docfamily = doc.EditFamily(fam);

            return docfamily;
        }


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

        /// <summary>
        /// Returns the first family symbol of a family
        /// </summary>
        /// <param name="family"></param>
        /// <returns>The first family symbol of the given family</returns>
        public static FamilySymbol GetFirstFamilySymbol(Family family)
        {
            ISet<ElementId> familySymbolIds = family.GetFamilySymbolIds();

            ElementId id = familySymbolIds.First();
            FamilySymbol familySymbol = family.Document.GetElement(id) as FamilySymbol;


            return familySymbol;
        }

    }
}
