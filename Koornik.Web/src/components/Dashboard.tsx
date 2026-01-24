/**
 * Main dashboard layout combining floor plan, events table, zones table, and device panel.
 */

import { useState, useEffect, useCallback, useRef } from 'react';
import { Layout, Typography, Badge, Spin, Alert, App, Splitter, Modal } from 'antd';
import { WifiOutlined, DisconnectOutlined, LoadingOutlined } from '@ant-design/icons';
import type { Polygon } from 'geojson';
import { FloorPlanMap } from './FloorPlanMap';
import { EventsTable } from './EventsTable';
import { ZonesTable } from './ZonesTable';
import { DevicePanel } from './DevicePanel';
import { DeviceDetailDrawer } from './DeviceDetailDrawer';
import { useKoornikHub } from '../hooks/useKoornikHub';
import { getDevices, getZones, getAllZoneStates, updateZone } from '../services/api';
import type { Device, Zone, ZoneState } from '../types';

const { Header, Content, Sider } = Layout;
const { Title } = Typography;
const { Panel } = Splitter;

/** Polling interval for zone states (in ms) */
const ZONE_STATE_POLL_INTERVAL = 30000;

export function Dashboard() {
  // Get message API from App context
  const { message } = App.useApp();

  // API data state
  const [devices, setDevices] = useState<Device[]>([]);
  const [zones, setZones] = useState<Zone[]>([]);
  const [zoneStates, setZoneStates] = useState<Map<string, ZoneState>>(new Map());
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // UI state
  const [selectedZoneId, setSelectedZoneId] = useState<string | null>(null);
  const [selectedDevice, setSelectedDevice] = useState<Device | null>(null);
  const [drawingZoneId, setDrawingZoneId] = useState<string | null>(null);

  // Track MACs we've already prompted about to avoid duplicate prompts
  const promptedMacsRef = useRef<Set<string>>(new Set());

  // Ref to the subscribe function from the hub so callback doesn't close over stale value
  const subscribeToDeviceRef = useRef<((mac: string) => Promise<void>) | null>(null);

  // Handler invoked for every DeviceStatus event from the hub
  const handleHubDeviceStatus = useCallback((event: any) => {
    const mac = event?.macAddress;
    if (!mac) return;

    // If we already know this device, or we've recently prompted for it, ignore
    const known = devices.some(d => d.macAddress === mac);
    if (known || promptedMacsRef.current.has(mac)) return;

    // Mark as prompted to avoid duplicates
    promptedMacsRef.current.add(mac);
    
    Modal.confirm({
      title: 'Unknown device detected',
      content: `Device with MAC ${mac} reported status. Add device and open details?`,
      onOk: async () => {
        // Create a minimal Device-like object and open the drawer
        const minimalDevice = ({ macAddress: mac } as unknown) as Device;
        setSelectedDevice(minimalDevice);
        try {
          await subscribeToDeviceRef.current?.(mac);
        } catch (err) {
          console.error('Failed to subscribe to device after opening drawer:', err);
        }
      }
    });
  }, [devices]);

  // SignalR hook
  const {
    connectionState,
    transitions,
    deviceStatuses,
    distanceReading,
    subscribeToDevice,
    unsubscribeFromDevice,
  } = useKoornikHub({ onDeviceStatus: handleHubDeviceStatus });

  // Keep a ref to the subscribe function for the modal handler
  useEffect(() => {
    subscribeToDeviceRef.current = subscribeToDevice;
  }, [subscribeToDevice]);

  // Load initial data
  const loadData = useCallback(async () => {
    try {
      setLoading(true);
      setError(null);

      const [devicesData, zonesData] = await Promise.all([
        getDevices(),
        getZones(),
      ]);

      console.log('Loaded zones:', zonesData);

      setDevices(devicesData);
      setZones(zonesData);

      // Load zone states
      const states = await getAllZoneStates(zonesData);
      setZoneStates(states);
    } catch (err) {
      console.error('Failed to load data:', err);
      setError(err instanceof Error ? err.message : 'Failed to load data');
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    loadData();
  }, [loadData]);

  // Poll zone states periodically
  useEffect(() => {
    if (zones.length === 0) return;

    const interval = setInterval(async () => {
      try {
        const states = await getAllZoneStates(zones);
        setZoneStates(states);
      } catch (err) {
        console.error('Failed to refresh zone states:', err);
      }
    }, ZONE_STATE_POLL_INTERVAL);

    return () => clearInterval(interval);
  }, [zones]);

  // Refresh zone states when a transition occurs
  useEffect(() => {
    if (transitions.length === 0 || zones.length === 0) return;

    const refreshStates = async () => {
      try {
        const states = await getAllZoneStates(zones);
        setZoneStates(states);
      } catch (err) {
        console.error('Failed to refresh zone states:', err);
      }
    };

    // Small delay to let backend process the transition
    const timeout = setTimeout(refreshStates, 500);
    return () => clearTimeout(timeout);
  }, [transitions[0]?.transitionId, zones]);

  // Handlers
  const handleZoneClick = useCallback((zoneId: string | null) => {
    setSelectedZoneId(zoneId);
  }, []);

  const handleClearZoneFilter = useCallback(() => {
    setSelectedZoneId(null);
  }, []);

  const handleDeviceClick = useCallback((device: Device) => {
    setSelectedDevice(device);
  }, []);

  const handleCloseDeviceDrawer = useCallback(() => {
    setSelectedDevice(null);
  }, []);

  // Zone drawing handlers
  const handleDrawZone = useCallback((zoneId: string) => {
    // Toggle drawing mode
    setDrawingZoneId(prev => (prev === zoneId ? null : zoneId));
  }, []);

  const handleZoneDrawn = useCallback(async (zoneId: string, geoJson: Polygon) => {
    const zone = zones.find(z => z.id === zoneId);
    if (!zone) return;

    try {
      const updatedZone = await updateZone({
        id: zoneId,
        name: zone.name,
        capacity: zone.capacity,
        geoJson,
      });
      
      // Update local state
      setZones(prev => prev.map(z => (z.id === zoneId ? updatedZone : z)));
      setDrawingZoneId(null);
      message.success(`Zone "${zone.name || 'Unnamed'}" boundary saved!`);
    } catch (err) {
      console.error('Failed to save zone boundary:', err);
      message.error('Failed to save zone boundary');
    }
  }, [zones, message]);

  const handleClearZoneGeoJson = useCallback(async (zoneId: string) => {
    const zone = zones.find(z => z.id === zoneId);
    if (!zone) return;

    try {
      const updatedZone = await updateZone({
        id: zoneId,
        name: zone.name,
        capacity: zone.capacity,
        geoJson: null,
      });
      setZones(prev => prev.map(z => (z.id === zoneId ? updatedZone : z)));
      message.success(`Zone "${zone.name || 'Unnamed'}" removed from map!`);
    } catch (err) {
      console.error('Failed to clear zone geoJson:', err);
      message.error('Failed to remove zone from map');
    }
  }, [zones, message]);

  // Connection status indicator
  const connectionBadge = (() => {
    switch (connectionState) {
      case 'connected':
        return <Badge status="success" text="Connected" />;
      case 'connecting':
        return <Badge status="processing" text="Connecting..." />;
      case 'reconnecting':
        return <Badge status="warning" text="Reconnecting..." />;
      case 'disconnected':
        return <Badge status="error" text="Disconnected" />;
    }
  })();

  if (loading) {
    return (
      <Spin
        indicator={<LoadingOutlined style={{ fontSize: 48 }} spin />}
        tip="Loading..."
        fullscreen
      />
    );
  }

  return (
    <Layout style={{ minHeight: '100vh' }}>
      {/* Header */}
      <Header
        style={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          padding: '0 24px',
          background: '#001529',
        }}
      >
        <Title level={4} style={{ margin: 0, color: '#fff' }}>
          Koornik Dashboard
        </Title>
        <div style={{ display: 'flex', alignItems: 'center', gap: 16 }}>
          {connectionState === 'connected' ? (
            <WifiOutlined style={{ color: '#52c41a', fontSize: 18 }} />
          ) : (
            <DisconnectOutlined style={{ color: '#ff4d4f', fontSize: 18 }} />
          )}
          {connectionBadge}
        </div>
      </Header>

      <Layout>
        {/* Main Content */}
        <Content style={{ display: 'flex', flexDirection: 'column', overflow: 'hidden' }}>
          {error && (
            <Alert
              message="Error loading data"
              description={error}
              type="error"
              showIcon
              closable
              style={{ margin: 16 }}
            />
          )}

          <Splitter orientation="vertical" style={{ flex: 1 }}>
            {/* Floor Plan Map */}
            <Panel defaultSize="60%" min="30%" collapsible>
              <div style={{ height: '100%', position: 'relative' }}>
                <FloorPlanMap
                  zones={zones}
                  zoneStates={zoneStates}
                  selectedZoneId={selectedZoneId}
                  drawingZoneId={drawingZoneId}
                  onZoneClick={handleZoneClick}
                  onZoneDrawn={handleZoneDrawn}
                />
                {drawingZoneId && (
                  <div
                    style={{
                      position: 'absolute',
                      top: 16,
                      left: '50%',
                      transform: 'translateX(-50%)',
                      background: 'rgba(0, 0, 0, 0.8)',
                      color: '#fff',
                      padding: '8px 16px',
                      borderRadius: 4,
                      zIndex: 1000,
                    }}
                  >
                    Click on the map to draw zone boundary. Click the first point (green) to complete.
                  </div>
                )}
              </div>
            </Panel>

            {/* Events and Zones Tables */}
            <Panel defaultSize="40%" min="200px" collapsible>
              <Splitter style={{ height: '100%', background: '#1f1f1f' }}>
                {/* Events Table */}
                <Panel defaultSize="60%" min="30%" style={{ padding: 16 }}>
                  <EventsTable
                    transitions={transitions}
                    zones={zones}
                    devices={devices}
                    filterZoneId={selectedZoneId}
                    onClearFilter={handleClearZoneFilter}
                  />
                </Panel>

                {/* Zones Table */}
                <Panel defaultSize="40%" min="300px" style={{ padding: 16 }}>
                  <ZonesTable
                    zones={zones}
                    devices={devices}
                    zoneStates={zoneStates}
                    selectedZoneId={selectedZoneId}
                    drawingZoneId={drawingZoneId}
                    onZoneSelect={handleZoneClick}
                    onDrawZone={handleDrawZone}
                    onClearZoneGeoJson={handleClearZoneGeoJson}
                    onRefresh={loadData}
                  />
                </Panel>
              </Splitter>
            </Panel>
          </Splitter>
        </Content>

        {/* Device Panel (Right Sider) */}
        <Sider
          width={320}
          theme="dark"
          style={{
            background: '#1f1f1f',
            borderLeft: '1px solid #303030',
          }}
        >
          <DevicePanel
            devices={devices}
            zones={zones}
            deviceStatuses={deviceStatuses}
            onDeviceClick={handleDeviceClick}
            onRefresh={loadData}
          />
        </Sider>
      </Layout>

      {/* Device Detail Drawer */}
      <DeviceDetailDrawer
        device={selectedDevice}
        zones={zones}
        zoneStates={zoneStates}
        deviceStatus={
          selectedDevice?.macAddress
            ? deviceStatuses.get(selectedDevice.macAddress)
            : undefined
        }
        distanceReading={distanceReading}
        onClose={handleCloseDeviceDrawer}
        onSubscribe={subscribeToDevice}
        onUnsubscribe={unsubscribeFromDevice}
        onDeviceSaved={(createdDevice) => {
          // Refresh device list and replace the minimal device with the created one
          loadData();
          if (createdDevice) {
            setSelectedDevice(createdDevice);
          }
        }}
      />
    </Layout>
  );
}
