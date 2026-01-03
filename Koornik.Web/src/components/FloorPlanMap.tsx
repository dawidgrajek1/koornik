/**
 * Floor plan map component using Leaflet with dynamic image bounds.
 * Displays zones as GeoJSON polygons with permanent occupancy tooltips.
 * Supports zone drawing mode for defining zone boundaries.
 */

import { useEffect, useState, useRef, useMemo, useCallback } from 'react';
import { MapContainer, ImageOverlay, GeoJSON, Tooltip, useMapEvents } from 'react-leaflet';
import L from 'leaflet';
import type { Polygon, Position } from 'geojson';
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
  /** Zone ID currently being drawn (null = not drawing) */
  drawingZoneId: string | null;
  /** Called when a zone is clicked */
  onZoneClick: (zoneId: string | null) => void;
  /** Called when zone drawing is completed */
  onZoneDrawn: (zoneId: string, geojson: Polygon) => void;
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

/** Drawing overlay component for creating polygon zones */
interface DrawingOverlayProps {
  drawingZoneId: string;
  onComplete: (zoneId: string, geojson: Polygon) => void;
}

function DrawingOverlay({ drawingZoneId, onComplete }: DrawingOverlayProps) {
  const [points, setPoints] = useState<L.LatLng[]>([]);
  const polylineRef = useRef<L.Polyline | null>(null);
  const markersRef = useRef<L.CircleMarker[]>([]);
  const map = useMapEvents({
    click(e) {
      const newPoints = [...points, e.latlng];
      setPoints(newPoints);
    },
  });

  // Draw the polygon preview
  useEffect(() => {
    // Clear previous drawings
    if (polylineRef.current) {
      map.removeLayer(polylineRef.current);
    }
    markersRef.current.forEach(m => map.removeLayer(m));
    markersRef.current = [];

    if (points.length > 0) {
      // Draw polyline connecting points
      const linePoints = [...points];
      if (points.length > 2) {
        linePoints.push(points[0]); // Close the polygon preview
      }
      polylineRef.current = L.polyline(linePoints, {
        color: '#1890ff',
        weight: 2,
        dashArray: '5, 5',
      }).addTo(map);

      // Draw markers at each point
      points.forEach((point, index) => {
        const isFirst = index === 0;
        const marker = L.circleMarker(point, {
          radius: isFirst ? 10 : 6,
          color: isFirst ? '#52c41a' : '#1890ff',
          fillColor: isFirst ? '#52c41a' : '#1890ff',
          fillOpacity: 0.8,
          weight: 2,
        }).addTo(map);

        // Click on first point to complete
        if (isFirst && points.length >= 3) {
          marker.on('click', (e) => {
            L.DomEvent.stopPropagation(e);
            completePolygon();
          });
          marker.bindTooltip('Click to complete', { permanent: false, direction: 'top' });
        }

        markersRef.current.push(marker);
      });
    }

    return () => {
      if (polylineRef.current) {
        map.removeLayer(polylineRef.current);
      }
      markersRef.current.forEach(m => map.removeLayer(m));
    };
  }, [points, map]);

  const completePolygon = useCallback(() => {
    if (points.length < 3) return;

    // Convert to GeoJSON polygon
    // Note: GeoJSON uses [lng, lat] order, Leaflet uses [lat, lng]
    const coordinates: Position[] = points.map(p => [p.lng, p.lat]);
    coordinates.push(coordinates[0]); // Close the ring

    const polygon: Polygon = {
      type: 'Polygon',
      coordinates: [coordinates],
    };

    onComplete(drawingZoneId, polygon);
    setPoints([]);
  }, [points, drawingZoneId, onComplete]);

  return null;
}

export function FloorPlanMap({
  zones,
  zoneStates,
  selectedZoneId,
  drawingZoneId,
  onZoneClick,
  onZoneDrawn,
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
    () => {
      const filtered = zones.filter(zone => zone.geoJson);
      if (filtered.length > 0) {
        console.log('Zones with GeoJSON:', filtered.map(z => ({ id: z.id, name: z.name, geoJson: z.geoJson })));
      }
      return filtered;
    },
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
        // Use geoJson hash in key to force re-render when polygon changes
        const geoKey = `${zone.id}-${JSON.stringify(zone.geoJson)}`;

        return (
          <GeoJSON
            key={geoKey}
            data={zone.geoJson!}
            style={() => getZoneStyle(zone.id)}
            coordsToLatLng={(coords) => {
              // GeoJSON stores [x, y] (lng, lat), convert to Leaflet LatLng for Simple CRS
              // In Simple CRS: lat = y, lng = x
              return L.latLng(coords[1], coords[0]);
            }}
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

      {/* Drawing overlay when in drawing mode */}
      {drawingZoneId && (
        <DrawingOverlay
          drawingZoneId={drawingZoneId}
          onComplete={onZoneDrawn}
        />
      )}
    </MapContainer>
  );
}
