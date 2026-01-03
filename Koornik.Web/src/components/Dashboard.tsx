/**
 * Main dashboard layout combining floor plan, events table, and device panel.
 */

import { useState, useEffect, useCallback } from 'react';
import { Layout, Typography, Badge, Spin, Alert } from 'antd';
import { WifiOutlined, DisconnectOutlined, LoadingOutlined } from '@ant-design/icons';
import { FloorPlanMap } from './FloorPlanMap';
import { EventsTable } from './EventsTable';
import { DevicePanel } from './DevicePanel';
import { DeviceDetailDrawer } from './DeviceDetailDrawer';
import { useKoornikHub } from '../hooks/useKoornikHub';
import { getDevices, getZones, getAllZoneStates } from '../services/api';
import type { Device, Zone, ZoneState } from '../types';

const { Header, Content, Sider } = Layout;
const { Title } = Typography;

/** Polling interval for zone states (in ms) */
const ZONE_STATE_POLL_INTERVAL = 30000;

export function Dashboard() {
  // API data state
  const [devices, setDevices] = useState<Device[]>([]);
  const [zones, setZones] = useState<Zone[]>([]);
  const [zoneStates, setZoneStates] = useState<Map<string, ZoneState>>(new Map());
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // UI state
  const [selectedZoneId, setSelectedZoneId] = useState<string | null>(null);
  const [selectedDevice, setSelectedDevice] = useState<Device | null>(null);

  // SignalR hook
  const {
    connectionState,
    transitions,
    deviceStatuses,
    distanceReading,
    subscribeToDevice,
    unsubscribeFromDevice,
  } = useKoornikHub();

  // Load initial data
  useEffect(() => {
    async function loadData() {
      try {
        setLoading(true);
        setError(null);

        const [devicesData, zonesData] = await Promise.all([
          getDevices(),
          getZones(),
        ]);

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
    }

    loadData();
  }, []);

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
              onClose={() => setError(null)}
            />
          )}

          {/* Floor Plan Map */}
          <div style={{ flex: 1, minHeight: 400 }}>
            <FloorPlanMap
              zones={zones}
              zoneStates={zoneStates}
              selectedZoneId={selectedZoneId}
              onZoneClick={handleZoneClick}
            />
          </div>

          {/* Events Table */}
          <div
            style={{
              height: 350,
              padding: 16,
              background: '#1f1f1f',
              borderTop: '1px solid #303030',
            }}
          >
            <EventsTable
              transitions={transitions}
              zones={zones}
              devices={devices}
              filterZoneId={selectedZoneId}
              onClearFilter={handleClearZoneFilter}
            />
          </div>
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
            deviceStatuses={deviceStatuses}
            onDeviceClick={handleDeviceClick}
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
      />
    </Layout>
  );
}
