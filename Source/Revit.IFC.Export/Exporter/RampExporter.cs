﻿//
// BIM IFC library: this library works with Autodesk(R) Revit(R) to export IFC files containing model geometry.
// Copyright (C) 2012-2016  Autodesk, Inc.
// 
// This library is free software; you can redistribute it and/or
// modify it under the terms of the GNU Lesser General Public
// License as published by the Free Software Foundation; either
// version 2.1 of the License, or (at your option) any later version.
//
// This library is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
// Lesser General Public License for more details.
//
// You should have received a copy of the GNU Lesser General Public
// License along with this library; if not, write to the Free Software
// Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301  USA
//

using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.IFC;
using Revit.IFC.Common.Enums;
using Revit.IFC.Common.Utility;
using Revit.IFC.Export.Utility;
using Revit.IFC.Export.Toolkit;

namespace Revit.IFC.Export.Exporter
{
   /// <summary>
   /// Provides methods to export ramps
   /// </summary>
   class RampExporter
   {
      /// <summary>
      /// Checks if exporting an element of Ramp category.
      /// </summary>
      /// <param name="element">The element.</param>
      /// <returns>True if element is of category OST_Ramps.</returns>
      static public bool IsRamp(Element element)
      {
         // FaceWall should be exported as IfcWall.
         return (CategoryUtil.GetSafeCategoryId(element) == new ElementId(BuiltInCategory.OST_Ramps));
      }

      static private double GetDefaultHeightForRamp()
      {
         // The default height for ramps is 3'.
         return UnitUtil.ScaleLength(3.0);
      }

      /// <summary>
      /// Gets the ramp height for a ramp.
      /// </summary>
      /// <param name="exporterIFC">
      /// The exporter.
      /// </param>
      /// <param name="element">
      /// The element.
      /// </param>
      /// <returns>
      /// The unscaled height.
      /// </returns>
      static public double GetRampHeight(ExporterIFC exporterIFC, Element element)
      {
         // Re-use the code for stairs height for legacy stairs.
         return StairsExporter.GetStairsHeightForLegacyStair(exporterIFC, element, GetDefaultHeightForRamp());
      }

      /// <summary>
      /// Gets the number of flights of a multi-story ramp.
      /// </summary>
      /// <param name="exporterIFC">
      /// The exporter.
      /// </param>
      /// <param name="element">
      /// The element.
      /// </param>
      /// <returns>
      /// The number of flights (at least 1.)
      /// </returns>
      static public int GetNumFlightsForRamp(ExporterIFC exporterIFC, Element element)
      {
         return StairsExporter.GetNumFlightsForLegacyStair(exporterIFC, element, GetDefaultHeightForRamp());
      }

      /// <summary>
      /// Gets IFCRampType from ramp type name.
      /// </summary>
      /// <param name="rampTypeName">The ramp type name.</param>
      /// <returns>The IFCRampType.</returns>
      public static string GetIFCRampType(string rampTypeName)
      {
         string typeName = NamingUtil.RemoveSpacesAndUnderscores(rampTypeName);

         if (String.Compare(typeName, "StraightRun", true) == 0 ||
             String.Compare(typeName, "StraightRunRamp", true) == 0)
            return "Straight_Run_Ramp";
         if (String.Compare(typeName, "TwoStraightRun", true) == 0 ||
             String.Compare(typeName, "TwoStraightRunRamp", true) == 0)
            return "Two_Straight_Run_Ramp";
         if (String.Compare(typeName, "QuarterTurn", true) == 0 ||
             String.Compare(typeName, "QuarterTurnRamp", true) == 0)
            return "Quarter_Turn_Ramp";
         if (String.Compare(typeName, "TwoQuarterTurn", true) == 0 ||
             String.Compare(typeName, "TwoQuarterTurnRamp", true) == 0)
            return "Two_Quarter_Turn_Ramp";
         if (String.Compare(typeName, "HalfTurn", true) == 0 ||
             String.Compare(typeName, "HalfTurnRamp", true) == 0)
            return "Half_Turn_Ramp";
         if (String.Compare(typeName, "Spiral", true) == 0 ||
             String.Compare(typeName, "SpiralRamp", true) == 0)
            return "Spiral_Ramp";
         if (String.Compare(typeName, "UserDefined", true) == 0)
            return "UserDefined";

         return "NotDefined";
      }

      /// <summary>
      /// Exports the top stories of a multistory ramp.
      /// </summary>
      /// <param name="exporterIFC">The ExporterIFC object.</param>
      /// <param name="ramp">The ramp element.</param>
      /// <param name="numFlights">The number of flights for a multistory ramp.</param>
      /// <param name="rampHnd">The stairs container handle.</param>
      /// <param name="components">The components handles.</param>
      /// <param name="ecData">The extrusion creation data.</param>
      /// <param name="componentECData">The extrusion creation data for the components.</param>
      /// <param name="placementSetter">The placement setter.</param>
      /// <param name="productWrapper">The ProductWrapper.</param>
      public static void ExportMultistoryRamp(ExporterIFC exporterIFC, Element ramp, int numFlights,
          IFCAnyHandle rampHnd, IList<IFCAnyHandle> components, IList<IFCExtrusionCreationData> componentECData,
          PlacementSetter placementSetter, ProductWrapper productWrapper)
      {
         if (numFlights < 2)
            return;

         double heightNonScaled = GetRampHeight(exporterIFC, ramp);
         if (heightNonScaled < MathUtil.Eps())
            return;

         if (IFCAnyHandleUtil.IsNullOrHasNoValue(rampHnd))
            return;

         IFCAnyHandle localPlacement = IFCAnyHandleUtil.GetObjectPlacement(rampHnd);
         if (IFCAnyHandleUtil.IsNullOrHasNoValue(localPlacement))
            return;

         IFCAnyHandle ownerHistory = ExporterCacheManager.OwnerHistoryHandle;

         IFCFile file = exporterIFC.GetFile();

         IFCAnyHandle relPlacement = GeometryUtil.GetRelativePlacementFromLocalPlacement(localPlacement);
         IFCAnyHandle ptHnd = IFCAnyHandleUtil.GetLocation(relPlacement);
         IList<double> origCoords = IFCAnyHandleUtil.GetCoordinates(ptHnd);

         IList<IFCAnyHandle> rampLocalPlacementHnds = new List<IFCAnyHandle>();
         IList<IFCLevelInfo> levelInfos = new List<IFCLevelInfo>();
         for (int ii = 0; ii < numFlights - 1; ii++)
         {
            IFCAnyHandle newLevelHnd = null;

            // We are going to avoid internal scaling routines, and instead scale in .NET.
            double newOffsetUnscaled = 0.0;
            IFCLevelInfo currLevelInfo =
                placementSetter.GetOffsetLevelInfoAndHandle(heightNonScaled * (ii + 1), 1.0, ramp.Document, out newLevelHnd, out newOffsetUnscaled);
            double newOffsetScaled = UnitUtil.ScaleLength(newOffsetUnscaled);

            if (currLevelInfo != null)
               levelInfos.Add(currLevelInfo);
            else
               levelInfos.Add(placementSetter.LevelInfo);

            XYZ orig;
            if (ptHnd.HasValue)
               orig = new XYZ(origCoords[0], origCoords[1], newOffsetScaled);
            else
               orig = new XYZ(0.0, 0.0, newOffsetScaled);

            rampLocalPlacementHnds.Add(ExporterUtil.CreateLocalPlacement(file, newLevelHnd, orig, null, null));
         }

         IList<List<IFCAnyHandle>> newComponents = new List<List<IFCAnyHandle>>();
         for (int ii = 0; ii < numFlights - 1; ii++)
            newComponents.Add(new List<IFCAnyHandle>());

         int compIdx = 0;
         ElementId catId = CategoryUtil.GetSafeCategoryId(ramp);

         foreach (IFCAnyHandle component in components)
         {
            string componentName = IFCAnyHandleUtil.GetStringAttribute(component, "Name");
            IFCAnyHandle componentProdRep = IFCAnyHandleUtil.GetInstanceAttribute(component, "Representation");

            IList<string> localComponentNames = new List<string>();
            IList<IFCAnyHandle> componentPlacementHnds = new List<IFCAnyHandle>();

            IFCAnyHandle localLocalPlacement = IFCAnyHandleUtil.GetObjectPlacement(component);
            IFCAnyHandle localRelativePlacement =
                (localLocalPlacement == null) ? null : IFCAnyHandleUtil.GetInstanceAttribute(localLocalPlacement, "RelativePlacement");

            bool isSubRamp = component.IsSubTypeOf(IFCEntityType.IfcRamp.ToString());
            for (int ii = 0; ii < numFlights - 1; ii++)
            {
               localComponentNames.Add((componentName == null) ? (ii + 2).ToString() : (componentName + ":" + (ii + 2)));
               if (isSubRamp)
                  componentPlacementHnds.Add(ExporterUtil.CopyLocalPlacement(file, rampLocalPlacementHnds[ii]));
               else
                  componentPlacementHnds.Add(IFCInstanceExporter.CreateLocalPlacement(file, rampLocalPlacementHnds[ii], localRelativePlacement));
            }

            IList<IFCAnyHandle> localComponentHnds = new List<IFCAnyHandle>();
            if (isSubRamp)
            {
               string componentType = IFCAnyHandleUtil.GetEnumerationAttribute(component, ExporterCacheManager.ExportOptionsCache.ExportAs4 ? "PredefinedType" : "ShapeType");
               string localRampType = GetIFCRampType(componentType);

               for (int ii = 0; ii < numFlights - 1; ii++)
               {
                  IFCAnyHandle representationCopy =
                      ExporterUtil.CopyProductDefinitionShape(exporterIFC, ramp, catId, componentProdRep);

                  IFCAnyHandle localComponent = IFCInstanceExporter.CreateRamp(exporterIFC, ramp, GUIDUtil.CreateGUID(), ownerHistory,
                      componentPlacementHnds[ii], representationCopy, localRampType);

                  localComponentHnds.Add(localComponent);
                  IFCAnyHandleUtil.OverrideNameAttribute(localComponent, localComponentNames[ii]);

               }
            }
            else if (IFCAnyHandleUtil.IsSubTypeOf(component, IFCEntityType.IfcRampFlight))
            {
               for (int ii = 0; ii < numFlights - 1; ii++)
               {
                  IFCAnyHandle representationCopy =
                      ExporterUtil.CopyProductDefinitionShape(exporterIFC, ramp, catId, componentProdRep);

                  IFCAnyHandle localComponent = IFCInstanceExporter.CreateRampFlight(exporterIFC, ramp, GUIDUtil.CreateGUID(), ownerHistory,
                      componentPlacementHnds[ii], representationCopy, "NOTDEFINED");

                  IFCAnyHandleUtil.OverrideNameAttribute(localComponent, localComponentNames[ii]);
                  localComponentHnds.Add(localComponent);
               }
            }
            else if (IFCAnyHandleUtil.IsSubTypeOf(component, IFCEntityType.IfcSlab))
            {
               string componentType = IFCAnyHandleUtil.GetEnumerationAttribute(component, "PredefinedType");
               IFCSlabType localLandingType = FloorExporter.GetIFCSlabType(componentType);

               for (int ii = 0; ii < numFlights - 1; ii++)
               {
                  IFCAnyHandle representationCopy =
                      ExporterUtil.CopyProductDefinitionShape(exporterIFC, ramp, catId, componentProdRep);

                  IFCAnyHandle localComponent = IFCInstanceExporter.CreateSlab(exporterIFC, ramp, GUIDUtil.CreateGUID(), ownerHistory,
                      componentPlacementHnds[ii], representationCopy, localLandingType.ToString());
                  localComponentHnds.Add(localComponent);
                  IFCAnyHandleUtil.OverrideNameAttribute(localComponent, localComponentNames[ii]);
               }
            }
            else if (IFCAnyHandleUtil.IsSubTypeOf(component, IFCEntityType.IfcMember))
            {
               for (int ii = 0; ii < numFlights - 1; ii++)
               {
                  IFCAnyHandle representationCopy =
                      ExporterUtil.CopyProductDefinitionShape(exporterIFC, ramp, catId, componentProdRep);

                  IFCAnyHandle localComponent = IFCInstanceExporter.CreateMember(exporterIFC, ramp, GUIDUtil.CreateGUID(), ownerHistory,
                componentPlacementHnds[ii], representationCopy, "STRINGER");
                  localComponentHnds.Add(localComponent);
                  IFCAnyHandleUtil.OverrideNameAttribute(localComponent, localComponentNames[ii]);
               }
            }

            for (int ii = 0; ii < numFlights - 1; ii++)
            {
               if (localComponentHnds[ii] != null)
               {
                  newComponents[ii].Add(localComponentHnds[ii]);
                  productWrapper.AddElement(null, localComponentHnds[ii], levelInfos[ii], componentECData[compIdx], false);
               }
            }
            compIdx++;
         }

         // finally add a copy of the container.
         IList<IFCAnyHandle> rampCopyHnds = new List<IFCAnyHandle>();
         for (int ii = 0; ii < numFlights - 1; ii++)
         {
            string rampTypeAsString = null;
            if (ExporterCacheManager.ExportOptionsCache.ExportAs4)
               rampTypeAsString = IFCAnyHandleUtil.GetEnumerationAttribute(rampHnd, "PredefinedType");
            else
               rampTypeAsString = IFCAnyHandleUtil.GetEnumerationAttribute(rampHnd, "ShapeType");
            string rampType = GetIFCRampType(rampTypeAsString);

            string containerRampName = IFCAnyHandleUtil.GetStringAttribute(rampHnd, "Name") + ":" + (ii + 2);
            IFCAnyHandle rampCopyHnd = IFCInstanceExporter.CreateRamp(exporterIFC, ramp, GUIDUtil.CreateGUID(), ownerHistory,
                rampLocalPlacementHnds[ii], null, rampType);

            rampCopyHnds.Add(rampCopyHnd);
            IFCAnyHandleUtil.OverrideNameAttribute(rampCopyHnd, containerRampName);

            productWrapper.AddElement(ramp, rampCopyHnds[ii], levelInfos[ii], null, true);
         }

         for (int ii = 0; ii < numFlights - 1; ii++)
         {
            StairRampContainerInfo stairRampInfo = new StairRampContainerInfo(rampCopyHnds[ii], newComponents[ii],
                rampLocalPlacementHnds[ii]);
            ExporterCacheManager.StairRampContainerInfoCache.AppendStairRampContainerInfo(ramp.Id, stairRampInfo);
         }
      }

      /// <summary>
      /// Exports a ramp to IfcRamp, without decomposing into separate runs and landings.
      /// </summary>
      /// <param name="exporterIFC">The ExporterIFC object.</param>
      /// <param name="ifcEnumType">The ramp type.</param>
      /// <param name="ramp">The ramp element.</param>
      /// <param name="geometryElement">The geometry element.</param>
      /// <param name="numFlights">The number of flights for a multistory ramp.</param>
      /// <param name="productWrapper">The ProductWrapper.</param>
      public static void ExportRamp(ExporterIFC exporterIFC, string ifcEnumType, Element ramp, GeometryElement geometryElement,
          int numFlights, ProductWrapper productWrapper)
      {
         if (ramp == null || geometryElement == null)
            return;

         // Check the intended IFC entity or type name is in the exclude list specified in the UI
         Common.Enums.IFCEntityType elementClassTypeEnum = Common.Enums.IFCEntityType.IfcRamp;
         if (ExporterCacheManager.ExportOptionsCache.IsElementInExcludeList(elementClassTypeEnum))
            return;

         // TESTING
         foreach (GeometryObject geomObj in geometryElement)
         {
            Visibility visibility = geomObj.Visibility;
         }

         IFCFile file = exporterIFC.GetFile();
         ElementId categoryId = CategoryUtil.GetSafeCategoryId(ramp);

         using (IFCTransaction tr = new IFCTransaction(file))
         {
            using (PlacementSetter placementSetter = PlacementSetter.Create(exporterIFC, ramp))
            {
               IFCAnyHandle contextOfItemsFootPrint = exporterIFC.Get3DContextHandle("FootPrint");
               IFCAnyHandle contextOfItemsAxis = exporterIFC.Get3DContextHandle("Axis");

               Transform trf = ExporterIFCUtils.GetUnscaledTransform(exporterIFC, placementSetter.LocalPlacement);
               IFCAnyHandle ownerHistory = ExporterCacheManager.OwnerHistoryHandle;

               IList<(Solid body, Face largestTopFace)> rampFlights = null;
               IList<Solid> landings = null;
               if (IdentifyRampFlightAndLanding(geometryElement, out rampFlights, out landings))
               {
                  string rampGUID = GUIDUtil.CreateGUID(ramp);
                  IFCAnyHandle rampLocalPlacement = placementSetter.LocalPlacement;
                  string predefType = "NOTDEFINED"; //Temporary
                  IFCAnyHandle rampContainerHnd = IFCInstanceExporter.CreateRamp(exporterIFC, ramp, rampGUID, ownerHistory, rampLocalPlacement, null, predefType);
                  // Create appropriate type
                  IFCExportInfoPair exportType = new IFCExportInfoPair();
                  exportType.SetValueWithPair(IFCEntityType.IfcRamp);
                  IFCAnyHandle rampTypeHnd = ExporterUtil.CreateGenericTypeFromElement(ramp, exportType, exporterIFC.GetFile(), ownerHistory, predefType, productWrapper);
                  ExporterCacheManager.TypeRelationsCache.Add(rampTypeHnd, rampContainerHnd);
                  productWrapper.AddElement(ramp, rampContainerHnd, placementSetter.LevelInfo, null, true);

                  //Breakdown the Ramp into its components: RampFlights and Landings
                  int rampFlightIndex = 0;
                  int landingIndex = 0;
                  HashSet<IFCAnyHandle> rampComponents = new HashSet<IFCAnyHandle>();
                  foreach ((Solid body, Face topFace) rampFlight in rampFlights)
                  {
                     using (IFCExtrusionCreationData ecData = new IFCExtrusionCreationData())
                     {
                        ecData.AllowVerticalOffsetOfBReps = false;
                        ecData.SetLocalPlacement(ExporterUtil.CreateLocalPlacement(file, placementSetter.LocalPlacement, null));
                        ecData.ReuseLocalPlacement = true;
                        BodyExporterOptions bodyExporterOptions = new BodyExporterOptions(true, ExportOptionsCache.ExportTessellationLevel.ExtraLow);
                        BodyData bodyData = BodyExporter.ExportBody(exporterIFC, ramp, categoryId, ElementId.InvalidElementId, rampFlight.body, bodyExporterOptions, ecData);

                        IFCAnyHandle bodyRep = bodyData.RepresentationHnd;
                        if (IFCAnyHandleUtil.IsNullOrHasNoValue(bodyRep))
                        {
                           ecData.ClearOpenings();
                           continue;
                        }
                        IList<IFCAnyHandle> reps = new List<IFCAnyHandle>();
                        reps.Add(bodyRep);

                        //if (!ExporterCacheManager.ExportOptionsCache.ExportAsCoordinationView2)
                        //{
                        //   CreateWalkingLineAndFootprint(exporterIFC, run, bodyData, categoryId, trf, ref reps);
                        //}

                        Transform boundingBoxTrf = (bodyData.OffsetTransform == null) ? Transform.Identity : bodyData.OffsetTransform.Inverse;
                        IList<GeometryObject> solidList = new List<GeometryObject>();
                        solidList.Add(rampFlight.body);
                        IFCAnyHandle boundingBoxRep = BoundingBoxExporter.ExportBoundingBox(exporterIFC, solidList, boundingBoxTrf);
                        if (boundingBoxRep != null)
                           reps.Add(boundingBoxRep);

                        IFCAnyHandle representation = IFCInstanceExporter.CreateProductDefinitionShape(exporterIFC.GetFile(), null, null, reps);

                        rampFlightIndex++;
                        string flightGUID = GUIDUtil.CreateSubElementGUID(ramp, rampFlightIndex);
                        string origFlightName = IFCAnyHandleUtil.GetStringAttribute(rampContainerHnd, "Name") + " " + rampFlightIndex;
                        string flightName = NamingUtil.GetOverrideStringValue(ramp, "IfcRampFlight.Name (" + rampFlightIndex + ")", origFlightName);

                        IFCAnyHandle flightLocalPlacement = ecData.GetLocalPlacement();
                        string flightPredefType = NamingUtil.GetOverrideStringValue(ramp, "IfcRampFlight.PredefinedType (" + rampFlightIndex + ")", null);

                        IFCAnyHandle rampFlightHnd = IFCInstanceExporter.CreateRampFlight(exporterIFC, null, flightGUID, ownerHistory, flightLocalPlacement,
                            representation, flightPredefType);
                        IFCAnyHandleUtil.OverrideNameAttribute(rampFlightHnd, flightName);
                        rampComponents.Add(rampFlightHnd);

                        // Create type
                        IFCExportInfoPair flightEportType = new IFCExportInfoPair();
                        flightEportType.SetValueWithPair(IFCEntityType.IfcRampFlight);
                        IFCAnyHandle flightTypeHnd = IFCInstanceExporter.CreateGenericIFCType(flightEportType, null, exporterIFC.GetFile(), null, null, flightPredefType);
                        IFCAnyHandleUtil.OverrideNameAttribute(flightTypeHnd, flightName);
                        ExporterCacheManager.TypeRelationsCache.Add(flightTypeHnd, rampFlightHnd);

                        CategoryUtil.CreateMaterialAssociation(exporterIFC, rampFlightHnd, bodyData.MaterialIds);

                        IFCAnyHandle psetRampFlightCommonHnd = CreatePSetRampFlightCommon(exporterIFC, file, ramp, rampFlightIndex, rampFlight.topFace);

                        if (!IFCAnyHandleUtil.IsNullOrHasNoValue(psetRampFlightCommonHnd))
                        {
                           HashSet<IFCAnyHandle> relatedObjects = new HashSet<IFCAnyHandle>() { rampFlightHnd };
                           ExporterUtil.CreateRelDefinesByProperties(file, GUIDUtil.CreateGUID(), ownerHistory, null, null, relatedObjects, psetRampFlightCommonHnd);
                        }
                     }
                  }
                  foreach (Solid landing in landings)
                  {
                     using (IFCExtrusionCreationData ecData = new IFCExtrusionCreationData())
                     {
                        ecData.AllowVerticalOffsetOfBReps = false;
                        ecData.SetLocalPlacement(ExporterUtil.CreateLocalPlacement(file, placementSetter.LocalPlacement, null));
                        ecData.ReuseLocalPlacement = true;
                        BodyExporterOptions bodyExporterOptions = new BodyExporterOptions(true, ExportOptionsCache.ExportTessellationLevel.ExtraLow);
                        BodyData bodyData = BodyExporter.ExportBody(exporterIFC, ramp, categoryId, ElementId.InvalidElementId, landing, bodyExporterOptions, ecData);

                        IFCAnyHandle bodyRep = bodyData.RepresentationHnd;
                        if (IFCAnyHandleUtil.IsNullOrHasNoValue(bodyRep))
                        {
                           ecData.ClearOpenings();
                           continue;
                        }
                        IList<IFCAnyHandle> reps = new List<IFCAnyHandle>();
                        reps.Add(bodyRep);

                        //if (!ExporterCacheManager.ExportOptionsCache.ExportAsCoordinationView2)
                        //{
                        //   CreateWalkingLineAndFootprint(exporterIFC, run, bodyData, categoryId, trf, ref reps);
                        //}

                        Transform boundingBoxTrf = (bodyData.OffsetTransform == null) ? Transform.Identity : bodyData.OffsetTransform.Inverse;
                        IList<GeometryObject> solidList = new List<GeometryObject>();
                        solidList.Add(landing);
                        IFCAnyHandle boundingBoxRep = BoundingBoxExporter.ExportBoundingBox(exporterIFC, solidList, boundingBoxTrf);
                        if (boundingBoxRep != null)
                           reps.Add(boundingBoxRep);

                        IFCAnyHandle representation = IFCInstanceExporter.CreateProductDefinitionShape(exporterIFC.GetFile(), null, null, reps);

                        landingIndex++;
                        string landingGUID = GUIDUtil.CreateSubElementGUID(ramp, landingIndex);
                        string origLandingName = IFCAnyHandleUtil.GetStringAttribute(rampContainerHnd, "Name") + " " + landingIndex;
                        string landingName = NamingUtil.GetOverrideStringValue(ramp, "IfcRampLanding.Name (" + landingIndex + ")", origLandingName);

                        IFCAnyHandle landingLocalPlacement = ecData.GetLocalPlacement();
                        string landingPredefType = "LANDING";

                        IFCAnyHandle rampLandingHnd = IFCInstanceExporter.CreateSlab(exporterIFC, ramp, landingGUID, ownerHistory, landingLocalPlacement,
                            representation, landingPredefType);
                        IFCAnyHandleUtil.OverrideNameAttribute(rampLandingHnd, landingName);
                        rampComponents.Add(rampLandingHnd);

                        // Create type
                        IFCExportInfoPair landingEportType = new IFCExportInfoPair();
                        landingEportType.SetValueWithPair(IFCEntityType.IfcSlab);
                        IFCAnyHandle landingTypeHnd = IFCInstanceExporter.CreateGenericIFCType(landingEportType, null, exporterIFC.GetFile(), null, null, landingPredefType);
                        IFCAnyHandleUtil.OverrideNameAttribute(landingTypeHnd, landingName);
                        ExporterCacheManager.TypeRelationsCache.Add(landingTypeHnd, rampLandingHnd);

                        CategoryUtil.CreateMaterialAssociation(exporterIFC, rampLandingHnd, bodyData.MaterialIds);

                        IFCAnyHandle psetSlabCommonHnd = CreatePSetRampLandingCommon(exporterIFC, file, ramp, landingIndex);

                        if (!IFCAnyHandleUtil.IsNullOrHasNoValue(psetSlabCommonHnd))
                        {
                           HashSet<IFCAnyHandle> relatedObjects = new HashSet<IFCAnyHandle>() { rampLandingHnd };
                           ExporterUtil.CreateRelDefinesByProperties(file, GUIDUtil.CreateGUID(), ownerHistory, null, null, relatedObjects, psetSlabCommonHnd);
                        }
                     }
                  }

                  if (rampComponents.Count > 0)
                  {
                     IFCInstanceExporter.CreateRelAggregates(file, GUIDUtil.CreateGUID(), ownerHistory, null, null, rampContainerHnd, rampComponents);
                  }
               }
               else
               {
                  using (IFCExtrusionCreationData ecData = new IFCExtrusionCreationData())
                  {
                     ecData.SetLocalPlacement(placementSetter.LocalPlacement);
                     ecData.ReuseLocalPlacement = false;

                     GeometryElement rampGeom = GeometryUtil.GetOneLevelGeometryElement(geometryElement, numFlights);

                     BodyData bodyData;

                     BodyExporterOptions bodyExporterOptions = new BodyExporterOptions(true, ExportOptionsCache.ExportTessellationLevel.ExtraLow);
                     IFCAnyHandle representation = RepresentationUtil.CreateAppropriateProductDefinitionShape(exporterIFC,
                         ramp, categoryId, rampGeom, bodyExporterOptions, null, ecData, out bodyData);

                     if (IFCAnyHandleUtil.IsNullOrHasNoValue(representation))
                     {
                        ecData.ClearOpenings();
                        return;
                     }

                     string containedRampGuid = GUIDUtil.CreateSubElementGUID(ramp, (int)IFCRampSubElements.ContainedRamp);
                     IFCAnyHandle containedRampLocalPlacement = ExporterUtil.CreateLocalPlacement(file, ecData.GetLocalPlacement(), null);
                     string rampType = GetIFCRampType(ifcEnumType);

                     List<IFCAnyHandle> components = new List<IFCAnyHandle>();
                     IList<IFCExtrusionCreationData> componentExtrusionData = new List<IFCExtrusionCreationData>();
                     IFCAnyHandle containedRampHnd = IFCInstanceExporter.CreateRamp(exporterIFC, ramp, containedRampGuid, ownerHistory,
                         containedRampLocalPlacement, representation, rampType);
                     components.Add(containedRampHnd);
                     componentExtrusionData.Add(ecData);
                     //productWrapper.AddElement(containedRampHnd, placementSetter.LevelInfo, ecData, false);
                     CategoryUtil.CreateMaterialAssociation(exporterIFC, containedRampHnd, bodyData.MaterialIds);

                     string guid = GUIDUtil.CreateGUID(ramp);
                     IFCAnyHandle localPlacement = ecData.GetLocalPlacement();

                     IFCAnyHandle rampHnd = IFCInstanceExporter.CreateRamp(exporterIFC, ramp, guid, ownerHistory,
                         localPlacement, null, rampType);

                     productWrapper.AddElement(ramp, rampHnd, placementSetter.LevelInfo, ecData, true);

                     StairRampContainerInfo stairRampInfo = new StairRampContainerInfo(rampHnd, components, localPlacement);
                     ExporterCacheManager.StairRampContainerInfoCache.AddStairRampContainerInfo(ramp.Id, stairRampInfo);

                     ExportMultistoryRamp(exporterIFC, ramp, numFlights, rampHnd, components, componentExtrusionData, placementSetter,
                         productWrapper);
                  }
               }
            }
            tr.Commit();
         }
      }

      /// <summary>
      /// Exports a ramp to IfcRamp.
      /// </summary>
      /// <param name="exporterIFC">The ExporterIFC object.</param>
      /// <param name="element">The ramp element.</param>
      /// <param name="geometryElement">The geometry element.</param>
      /// <param name="productWrapper">The ProductWrapper.</param>
      public static void Export(ExporterIFC exporterIFC, Element element, GeometryElement geometryElement, ProductWrapper productWrapper)
      {
         string ifcEnumType = ExporterUtil.GetIFCTypeFromExportTable(exporterIFC, element);
         IFCFile file = exporterIFC.GetFile();

         using (IFCTransaction tr = new IFCTransaction(file))
         {
            StairsExporter.ExportLegacyStairOrRampAsContainer(exporterIFC, ifcEnumType, element, geometryElement, productWrapper);

            // If we didn't create a handle here, then the element wasn't a "native" Ramp, and is likely a FamilyInstance or a DirectShape.
            if (IFCAnyHandleUtil.IsNullOrHasNoValue(productWrapper.GetAnElement()))
            {
               int numFlights = GetNumFlightsForRamp(exporterIFC, element);
               if (numFlights > 0)
                  ExportRamp(exporterIFC, ifcEnumType, element, geometryElement, numFlights, productWrapper);
            }

            tr.Commit();
         }
      }


      static bool IdentifyRampFlightAndLanding(GeometryElement rampGeom, out IList<(Solid body, Face largestTopFace)> rampFlights, out IList<Solid> landings)
      {
         rampFlights = new List<(Solid, Face)>();
         landings = new List<Solid>();

         if (!ExporterCacheManager.ExportOptionsCache.ExportAs2x2)
         {
            SolidMeshGeometryInfo info = GeometryUtil.GetSplitSolidMeshGeometry(rampGeom, Transform.Identity);
            //IList<Mesh> meshes = info.GetMeshes();
            //if (meshes.Count == 0)
            //{
               IList<Solid> solidList = info.GetSolids();
               foreach (Solid solid in solidList)
               {
                  // Determine the largest face and with normal pointing to upward (+Z region). If the normal is exactly at +Z (0,0,1), then it should be landing
                  Face rampComponentFace = GeometryUtil.GetLargestFaceInSolid(solid, new XYZ(0,0,1));
                  if (rampComponentFace == null)
                     continue;

                  XYZ normal = rampComponentFace.ComputeNormal(new UV());
                  if (MathUtil.IsAlmostEqual(normal.Z, 1.0))
                     landings.Add(solid);
                  else
                     rampFlights.Add((body:solid, largestTopFace:rampComponentFace));
               }
            //}
         }

         if (rampFlights.Count == 0 && landings.Count == 0)
            return false;

         return true;
      }

      private static IFCAnyHandle CreatePSetRampFlightCommon(ExporterIFC exporterIFC, IFCFile file, Element element, int flightIndex, Face topFace)
      {
         HashSet<IFCAnyHandle> properties = new HashSet<IFCAnyHandle>();

         string stringParam = "";
         if (ParameterUtil.GetStringValueFromElement(element, "Pset_RampFlightCommon.Reference (" + flightIndex.ToString() + ")", out stringParam) != null)
         {
            IFCData paramVal = Revit.IFC.Export.Toolkit.IFCDataUtil.CreateAsIdentifier(stringParam);
            IFCAnyHandle propSingleValue = IFCInstanceExporter.CreatePropertySingleValue(file, "Reference", null, paramVal, null);
            properties.Add(propSingleValue);
         }

         double doubleParam = 0.0;
         if (ParameterUtil.GetDoubleValueFromElement(element, null, "Pset_RampFlightCommon.HeadRoom (" + flightIndex.ToString() + ")", out doubleParam) != null)
         {
            IFCData paramVal = Revit.IFC.Export.Toolkit.IFCDataUtil.CreateAsPositiveLengthMeasure(doubleParam);
            IFCAnyHandle propSingleValue = IFCInstanceExporter.CreatePropertySingleValue(file, "Headroom", null, paramVal, null);
            properties.Add(propSingleValue);
         }

         // Slope
         double slope = 0.0;
         if (topFace != null)
         {
            XYZ faceNormal = topFace.ComputeNormal(new UV());
            XYZ projectionToXYPlane = new XYZ(faceNormal.X, faceNormal.Y, 0);
            slope = GeometryUtil.GetAngleOfFace(topFace, projectionToXYPlane);
         }

         double doubleParamOverride = 0.0;
         if (ParameterUtil.GetDoubleValueFromElement(element, null, "Pset_RampFlightCommon.Slope (" + flightIndex.ToString() + ")", out doubleParamOverride) != null)
         {
            slope = doubleParamOverride;
         }

         if (!MathUtil.IsAlmostZero(slope))
         { 
            IFCData paramVal = Revit.IFC.Export.Toolkit.IFCDataUtil.CreateAsPositiveLengthMeasure(slope);
            IFCAnyHandle propSingleValue = IFCInstanceExporter.CreatePropertySingleValue(file, "Slope", null, paramVal, null);
            properties.Add(propSingleValue);
         }
         // Slope

         if (!ExporterCacheManager.ExportOptionsCache.ExportAsOlderThanIFC4)
         {
            if (ParameterUtil.GetStringValueFromElement(element, "Pset_RampFlightCommon.Status (" + flightIndex.ToString() + ")", out stringParam) != null)
            {
               IFCData paramVal = Revit.IFC.Export.Toolkit.IFCDataUtil.CreateAsLabel(stringParam);
               IFCAnyHandle propSingleValue = IFCInstanceExporter.CreatePropertySingleValue(file, "Status", null, paramVal, null);
               properties.Add(propSingleValue);
            }

            if (ParameterUtil.GetDoubleValueFromElement(element, null, "Pset_RampFlightCommon.ClearWidth (" + flightIndex.ToString() + ")", out doubleParam) != null)
            {
               IFCData paramVal = Revit.IFC.Export.Toolkit.IFCDataUtil.CreateAsPositiveLengthMeasure(doubleParam);
               IFCAnyHandle propSingleValue = IFCInstanceExporter.CreatePropertySingleValue(file, "ClearWidth", null, paramVal, null);
               properties.Add(propSingleValue);
            }

            if (!MathUtil.IsAlmostZero(slope))
               doubleParam = UnitUtil.ScaleAngle(Math.PI / 2.0) - slope;
            if (ParameterUtil.GetDoubleValueFromElement(element, null, "Pset_RampFlightCommon.CounterSlope (" + flightIndex.ToString() + ")", out doubleParamOverride) != null)
               doubleParam = doubleParamOverride;

            if (!MathUtil.IsAlmostZero(doubleParam))
            {
               IFCData paramVal = Revit.IFC.Export.Toolkit.IFCDataUtil.CreateAsPositiveLengthMeasure(doubleParam);
               IFCAnyHandle propSingleValue = IFCInstanceExporter.CreatePropertySingleValue(file, "CounterSlope", null, paramVal, null);
               properties.Add(propSingleValue);
            }
         }

         if (properties.Count > 0)
         {
            return IFCInstanceExporter.CreatePropertySet(file,
                GUIDUtil.CreateGUID(), ExporterCacheManager.OwnerHistoryHandle, "Pset_RampFlightCommon", null, properties);
         }

         return null;
      }

      private static IFCAnyHandle CreatePSetRampLandingCommon(ExporterIFC exporterIFC, IFCFile file, Element element, int flightIndex)
      {
         HashSet<IFCAnyHandle> properties = new HashSet<IFCAnyHandle>();

         string stringParam = "";
         if (ParameterUtil.GetStringValueFromElement(element, "Pset_SlabCommon.Reference (" + flightIndex.ToString() + ")", out stringParam) != null)
         {
            IFCData paramVal = Revit.IFC.Export.Toolkit.IFCDataUtil.CreateAsIdentifier(stringParam);
            IFCAnyHandle propSingleValue = IFCInstanceExporter.CreatePropertySingleValue(file, "Reference", null, paramVal, null);
            properties.Add(propSingleValue);
         }

         if (ParameterUtil.GetStringValueFromElement(element, "Pset_SlabCommon.AcousticRating (" + flightIndex.ToString() + ")", out stringParam) != null)
         {
            IFCData paramVal = Revit.IFC.Export.Toolkit.IFCDataUtil.CreateAsLabel(stringParam);
            IFCAnyHandle propSingleValue = IFCInstanceExporter.CreatePropertySingleValue(file, "AcousticRating", null, paramVal, null);
            properties.Add(propSingleValue);
         }

         if (ParameterUtil.GetStringValueFromElement(element, "Pset_SlabCommon.FireRating (" + flightIndex.ToString() + ")", out stringParam) != null)
         {
            IFCData paramVal = Revit.IFC.Export.Toolkit.IFCDataUtil.CreateAsLabel(stringParam);
            IFCAnyHandle propSingleValue = IFCInstanceExporter.CreatePropertySingleValue(file, "FireRating", null, paramVal, null);
            properties.Add(propSingleValue);
         }

         if (ParameterUtil.GetStringValueFromElement(element, "Pset_SlabCommon.SurfaceSpreadOfFlame (" + flightIndex.ToString() + ")", out stringParam) != null)
         {
            IFCData paramVal = Revit.IFC.Export.Toolkit.IFCDataUtil.CreateAsLabel(stringParam);
            IFCAnyHandle propSingleValue = IFCInstanceExporter.CreatePropertySingleValue(file, "SurfaceSpreadOfFlame", null, paramVal, null);
            properties.Add(propSingleValue);
         }

         double doubleParam = 0.0;
         // Skip PitchAngle, it does not write the property as it should be 0 (a criteria for Landing)
         if (ParameterUtil.GetDoubleValueFromElement(element, null, "Pset_SlabCommon.ThermalTransmittance (" + flightIndex.ToString() + ")", out doubleParam) != null)
         {
            IFCData paramVal = Revit.IFC.Export.Toolkit.IFCDataUtil.CreateAsThermalTransmittanceMeasure(doubleParam);
            IFCAnyHandle propSingleValue = IFCInstanceExporter.CreatePropertySingleValue(file, "ThermalTransmittance", null, paramVal, null);
            properties.Add(propSingleValue);
         }

         int intParam = 0;
         if (ParameterUtil.GetIntValueFromElement(element, "Pset_SlabCommon.Combustible (" + flightIndex.ToString() + ")", out intParam) != null)
         {
            IFCData paramVal = Revit.IFC.Export.Toolkit.IFCDataUtil.CreateAsBoolean((intParam != 0)? true : false);
            IFCAnyHandle propSingleValue = IFCInstanceExporter.CreatePropertySingleValue(file, "Combustible", null, paramVal, null);
            properties.Add(propSingleValue);
         }

         if (ParameterUtil.GetIntValueFromElement(element, "Pset_SlabCommon.Compartmentation (" + flightIndex.ToString() + ")", out intParam) != null)
         {
            IFCData paramVal = Revit.IFC.Export.Toolkit.IFCDataUtil.CreateAsBoolean((intParam != 0) ? true : false);
            IFCAnyHandle propSingleValue = IFCInstanceExporter.CreatePropertySingleValue(file, "Compartmentation", null, paramVal, null);
            properties.Add(propSingleValue);
         }

         if (ParameterUtil.GetIntValueFromElement(element, "Pset_SlabCommon.IsExternal (" + flightIndex.ToString() + ")", out intParam) != null)
         {
            IFCData paramVal = Revit.IFC.Export.Toolkit.IFCDataUtil.CreateAsBoolean((intParam != 0) ? true : false);
            IFCAnyHandle propSingleValue = IFCInstanceExporter.CreatePropertySingleValue(file, "IsExternal", null, paramVal, null);
            properties.Add(propSingleValue);
         }

         if (ParameterUtil.GetIntValueFromElement(element, "Pset_SlabCommon.LoadBearing (" + flightIndex.ToString() + ")", out intParam) != null)
         {
            IFCData paramVal = Revit.IFC.Export.Toolkit.IFCDataUtil.CreateAsBoolean((intParam != 0) ? true : false);
            IFCAnyHandle propSingleValue = IFCInstanceExporter.CreatePropertySingleValue(file, "LoadBearing", null, paramVal, null);
            properties.Add(propSingleValue);
         }

         if (!ExporterCacheManager.ExportOptionsCache.ExportAsOlderThanIFC4)
         {
            if (ParameterUtil.GetStringValueFromElement(element, "Pset_SlabCommon.Status (" + flightIndex.ToString() + ")", out stringParam) != null)
            {
               IFCData paramVal = Revit.IFC.Export.Toolkit.IFCDataUtil.CreateAsLabel(stringParam);
               IFCAnyHandle propSingleValue = IFCInstanceExporter.CreatePropertySingleValue(file, "Status", null, paramVal, null);
               properties.Add(propSingleValue);
            }
         }

         if (properties.Count > 0)
         {
            return IFCInstanceExporter.CreatePropertySet(file,
                GUIDUtil.CreateGUID(), ExporterCacheManager.OwnerHistoryHandle, "Pset_SlabCommon", null, properties);
         }

         return null;
      }

   }
}