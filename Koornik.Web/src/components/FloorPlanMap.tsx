/**
 * Floor plan map component using Leaflet with dynamic image bounds.
 * Displays zones as GeoJSON polygons with permanent occupancy tooltips.
 */

import { useEffect, useState, useRef, useMemo } from 'react';
import { MapContainer, ImageOverlay, GeoJSON, Tooltip } from 'react-leaflet';
import L from 'leaflet';
import type { Zone, ZoneState } from '../types';
import 'leaflet/dist/leaflet.css';

// Floor plan image path (in public folder)
const FLOOR_PLAN_URL = '/floorplan.png';

interface FloorPlanMapProps {
  /** Available zones with optional GeoJSON */
  zones: Zone[];
  /** Current zone states (occupancy data) */
  zoneStates: Map<string, ZoneState>;
  /** Currently selected zone ID */
  selectedZoneId: string | null;
  /** Called when a zone is clicked */
  onZoneClick: (zoneId: string | null) => void;
}

interface ImageDimensions {
  width: number;
  height: number;
}

/** Get color based on occupancy percentage */
function getOccupancyColor(percentage: number): string {
  if (percentage >= 90) return '#ff4d4f'; // Red - critical
  if (percentage >= 70) return '#faad14'; // Orange - warning
  if (percentage >= 50) return '#fadb14'; // Yellow - moderate
  return '#52c41a'; // Green - good
}

export function FloorPlanMap({
  zones,
  zoneStates,
  selectedZoneId,
  onZoneClick,
}: FloorPlanMapProps) {
  const [imageDimensions, setImageDimensions] = useState<ImageDimensions | null>(null);
  const mapRef = useRef<L.Map | null>(null);

  // Load image to get dimensions
  useEffect(() => {
    const img = new Image();
    img.onload = () => {
      setImageDimensions({ width: img.width, height: img.height });
    };
    img.onerror = () => {
      console.error('Failed to load floor plan image');
      // Fallback dimensions
      setImageDimensions({ width: 1920, height: 1080 });
    };
    img.src = FLOOR_PLAN_URL;
  }, []);

  // Calculate bounds based on image dimensions
  const bounds = useMemo<L.LatLngBoundsExpression | null>(() => {
    if (!imageDimensions) return null;
    // Using Simple CRS: y increases upward, so we flip height
    return [
      [0, 0],
      [imageDimensions.height, imageDimensions.width],
    ];
  }, [imageDimensions]);

  // Filter zones that have GeoJSON data
  const zonesWithGeoJson = useMemo(
    () => zones.filter(zone => zone.geojson),
    [zones]
  );

  // Style function for GeoJSON features
  const getZoneStyle = (zoneId: string) => {
    const state = zoneStates.get(zoneId);
    const isSelected = zoneId === selectedZoneId;
    const occupancyPercentage = state?.occupancyPercentage ?? 0;

    return {
      fillColor: getOccupancyColor(occupancyPercentage),
      fillOpacity: isSelected ? 0.6 : 0.4,
      color: isSelected ? '#1890ff' : '#333',
      weight: isSelected ? 3 : 1,
    };
  };

  if (!imageDimensions || !bounds) {
    return (
      <div
        style={{
          height: '100%',
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          background: '#f0f2f5',
        }}
      >
        Loading floor plan...
      </div>
    );
  }

  return (
    <MapContainer
      ref={mapRef}
      crs={L.CRS.Simple}
      bounds={bounds}
      maxBounds={bounds}
      maxBoundsViscosity={1.0}
      style={{ height: '100%', width: '100%', background: '#1f1f1f' }}
      zoomControl={true}
      attributionControl={false}
      minZoom={-2}
      maxZoom={2}
    >
      <ImageOverlay url={FLOOR_PLAN_URL} bounds={bounds} />

      {/* Render zone polygons with occupancy tooltips */}
      {zonesWithGeoJson.map(zone => {
        const state = zoneStates.get(zone.id);
        const occupancy = state?.occupancyCount ?? 0;
        const capacity = state?.capacity ?? zone.capacity;
        const percentage = state?.occupancyPercentage ?? 0;

        return (
          <GeoJSON
            key={zone.id}
            data={zone.geojson!}
            style={() => getZoneStyle(zone.id)}
            eventHandlers={{
              click: () => {
                // Toggle selection
                onZoneClick(selectedZoneId === zone.id ? null : zone.id);
              },
            }}
          >
            <Tooltip permanent direction="center" className="zone-tooltip">
              <div style={{ textAlign: 'center' }}>
                <strong>{zone.name || 'Unnamed Zone'}</strong>
                <br />
                <span
                  style={{
                    color: getOccupancyColor(percentage),
                    fontWeight: 'bold',
                  }}
                >
                  {occupancy}/{capacity}
                </span>
                <span style={{ marginLeft: 4, fontSize: '0.85em' }}>
                  ({percentage.toFixed(0)}%)
                </span>
              </div>
            </Tooltip>
          </GeoJSON>
        );
      })}
    </MapContainer>
  );
}
