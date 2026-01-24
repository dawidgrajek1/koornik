/**
 * Device detail drawer component showing device info and live distance readings.
 */

import { useEffect, useMemo, useState, useCallback } from 'react';
import { Drawer, Descriptions, Badge, Statistic, Row, Col, Typography, Divider, Card, Button, Space, App, Alert } from 'antd';
import { DeviceFormModal } from './admin/DeviceFormModal';
import { createDevice } from '../services/api';
import { WifiOutlined, DashboardOutlined, EnvironmentOutlined, ToolOutlined, ThunderboltOutlined } from '@ant-design/icons';
import type { Device, Zone, ZoneState, DeviceStatusEvent, DistanceReadingEvent } from '../types';
import { calibrateDevice, setDistanceStream } from '../services/api';

const { Text } = Typography;

interface DeviceDetailDrawerProps {
  /** Device to display (null = drawer closed) */
  device: Device | null;
  /** All zones for name lookup */
  zones: Zone[];
  /** Zone states for occupancy info */
  zoneStates: Map<string, ZoneState>;
  /** Device status from SignalR */
  deviceStatus: DeviceStatusEvent | undefined;
  /** Live distance reading from SignalR */
  distanceReading: DistanceReadingEvent | null;
  /** Called when drawer should close */
  onClose: () => void;
  /** Called to subscribe to device distance readings */
  onSubscribe: (macAddress: string) => void;
  /** Called to unsubscribe from device distance readings */
  onUnsubscribe: (macAddress: string) => void;
  /** Optional callback called after a device is created (so parent can refresh list).
   * If provided, the created `Device` will be passed to the callback. */
  onDeviceSaved?: (device?: Device) => void;
}

export function DeviceDetailDrawer({
  device,
  zones,
  zoneStates,
  deviceStatus,
  distanceReading,
  onClose,
  onSubscribe,
  onUnsubscribe,
  onDeviceSaved,
}: DeviceDetailDrawerProps) {
  const { message } = App.useApp();
  const [calibrating, setCalibrating] = useState(false);
  const [streamLoading, setStreamLoading] = useState(false);
  const [createModalOpen, setCreateModalOpen] = useState(false);
  const [createLoading, setCreateLoading] = useState(false);

  const handleCreateSubmit = useCallback(async (values: any) => {
    setCreateLoading(true);
    try {
      const created = await createDevice(values);
      message.success('Device created successfully');
      setCreateModalOpen(false);
      // Pass created device back to parent so it can replace the minimal device in the drawer
      onDeviceSaved?.(created);
    } catch (err) {
      message.error(`Failed to create device: ${err instanceof Error ? err.message : 'Unknown error'}`);
    } finally {
      setCreateLoading(false);
    }
  }, [message, onDeviceSaved]);

  const handleCreateCancel = useCallback(() => {
    setCreateModalOpen(false);
  }, []);

  const handleCalibrate = useCallback(async () => {
    if (!device || !device.id) {
      message.error('Device must be registered to calibrate.');
      return;
    }
    setCalibrating(true);
    try {
      await calibrateDevice(device.id);
      message.success(`Calibration command sent to ${device.name || 'device'}`);
    } catch (error) {
      message.error(`Failed to calibrate: ${error instanceof Error ? error.message : 'Unknown error'}`);
    } finally {
      setCalibrating(false);
    }
  }, [device, message]);

  const handleDisableDistanceStream = useCallback(async () => {
    if (!device || !device.id) {
      message.error('Device must be registered to change streaming.');
      return;
    }
    setStreamLoading(true);
    try {
      await setDistanceStream(device.id, false);
      message.success(`Distance streaming disabled for ${device.name || 'device'}`);
    } catch (error) {
      message.error(`Failed to disable distance stream: ${error instanceof Error ? error.message : 'Unknown error'}`);
    } finally {
      setStreamLoading(false);
    }
  }, [device, message]);

  // Create zone name lookup
  const zoneNameMap = useMemo(() => {
    const map = new Map<string, string>();
    zones.forEach(zone => {
      map.set(zone.id, zone.name || 'Unknown');
    });
    return map;
  }, [zones]);

  // Subscribe/unsubscribe to device when opened/closed
  useEffect(() => {
    if (device?.macAddress) {
      onSubscribe(device.macAddress);
      return () => {
        onUnsubscribe(device.macAddress!);
      };
    }
  }, [device?.macAddress, onSubscribe, onUnsubscribe]);

  // Auto-enable distance streaming when drawer opens, disable when closed
  useEffect(() => {
    if (device?.id && deviceStatus && !deviceStatus.distanceStreamEnabled) {
      setDistanceStream(device.id, true).catch(() => {
        // Silently fail - user can see streaming status in UI
      });
    }
    
    // Disable streaming when drawer closes
    const deviceId = device?.id;
    return () => {
      if (deviceId) {
        setDistanceStream(deviceId, false).catch(() => {
          // Silently fail
        });
      }
    };
  }, [device?.id]);

  // Get zone info (handle null for border devices)
  const frontZone = device?.frontZoneId ? zones.find(z => z.id === device.frontZoneId) : null;
  const backZone = device?.backZoneId ? zones.find(z => z.id === device.backZoneId) : null;
  const frontZoneState = device?.frontZoneId ? zoneStates.get(device.frontZoneId) : null;
  const backZoneState = device?.backZoneId ? zoneStates.get(device.backZoneId) : null;

  // Check if device is offline (no status in last 60 seconds)
  const isStale = (() => {
    if (!deviceStatus?.timestamp) return false;
    const lastSeen = new Date(deviceStatus.timestamp);
    const now = new Date();
    return now.getTime() - lastSeen.getTime() > 60000;
  })();

  // Get status info
  const statusText = isStale ? 'Offline' : (deviceStatus?.state || 'Unknown');
  const statusColor = (() => {
    if (isStale) return '#8c8c8c';
    const state = statusText.toUpperCase();
    switch (state) {
      case 'IDLE':
      case 'ONLINE':
      case 'CALIBRATED':
      case 'SETTINGS_UPDATED':
        return '#52c41a';
      case 'DETECTING':
      case 'CALIBRATING':
        return '#faad14';
      case 'ERROR':
      case 'CALIBRATION_FAILED':
        return '#ff4d4f';
      default:
        return '#8c8c8c';
    }
  })();
  const isDeviceOffline = !deviceStatus || isStale || statusText.toUpperCase() === 'ERROR' || statusText.toUpperCase() === 'CALIBRATION_FAILED' || statusText === 'Unknown';

  return (
    <Drawer
      title={
        <span>
          <WifiOutlined style={{ marginRight: 8 }} />
          {device?.name || 'Device Details'}
        </span>
      }
      placement="right"
      size="default"
      open={!!device}
      onClose={onClose}
    >
      {device && (
        <>
          {!device.id && (
            <div style={{ marginBottom: 12, marginTop: 12 }}>
              <Button type="primary" onClick={() => setCreateModalOpen(true)} block>
                Add device to system
              </Button>
            </div>
          )}
          {/* Device Info */}
          <Descriptions column={1} size="small" bordered>
            <Descriptions.Item label="Name">
              {device.name || 'Unnamed'}
            </Descriptions.Item>
            <Descriptions.Item label="MAC Address">
              <Text code>{device.macAddress || 'N/A'}</Text>
            </Descriptions.Item>
            <Descriptions.Item label="IP Address">
              <Text code>{device.ipAddress || 'N/A'}</Text>
            </Descriptions.Item>
            <Descriptions.Item label="Status">
              <Badge color={statusColor} text={statusText} />
            </Descriptions.Item>
          </Descriptions>

          {!device.id && (
            <div style={{ marginTop: 12 }}>
              <Alert
                message="Unregistered device"
                description="This device is not registered in the system. You can view live readings but cannot perform device-level actions until it's added."
                type="warning"
                showIcon
              />
            </div>
          )}

          {/* Device Actions */}
          <Space style={{ marginTop: 16, width: '100%' }} direction="vertical">
            <Button
              icon={<ToolOutlined />}
              loading={calibrating}
              disabled={isDeviceOffline || !device.id}
              onClick={handleCalibrate}
              block
            >
              Calibrate Sensors
            </Button>
            <Button
              icon={<ThunderboltOutlined />}
              loading={streamLoading}
              disabled={isDeviceOffline || !device.id}
              onClick={handleDisableDistanceStream}
              block
            >
              Disable Distance Streaming
            </Button>
          </Space>

          <Divider>
            <EnvironmentOutlined /> Connected Zones
          </Divider>

          {/* Front Zone */}
          <Card size="small" style={{ marginBottom: 12 }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
              <div>
                <Text type="secondary">Front Zone</Text>
                <div>
                  {device.frontZoneId ? (
                    <Text strong>{frontZone?.name || zoneNameMap.get(device.frontZoneId) || 'Unknown'}</Text>
                  ) : (
                    <Text style={{ fontStyle: 'italic', color: '#888' }}>Outside</Text>
                  )}
                </div>
              </div>
              {frontZoneState && (
                <Statistic
                  title="Occupancy"
                  value={frontZoneState.occupancyCount}
                  suffix={`/ ${frontZoneState.capacity}`}
                  styles={{ content: { fontSize: '1rem' } }}
                />
              )}
            </div>
          </Card>

          {/* Back Zone */}
          <Card size="small">
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
              <div>
                <Text type="secondary">Back Zone</Text>
                <div>
                  {device.backZoneId ? (
                    <Text strong>{backZone?.name || zoneNameMap.get(device.backZoneId) || 'Unknown'}</Text>
                  ) : (
                    <Text style={{ fontStyle: 'italic', color: '#888' }}>Outside</Text>
                  )}
                </div>
              </div>
              {backZoneState && (
                <Statistic
                  title="Occupancy"
                  value={backZoneState.occupancyCount}
                  suffix={`/ ${backZoneState.capacity}`}
                  styles={{ content: { fontSize: '1rem' } }}
                />
              )}
            </div>
          </Card>

          {/* Thresholds */}
          {deviceStatus && (deviceStatus.thresholdFront !== null || deviceStatus.thresholdBack !== null) && (
            <>
              <Divider>Thresholds</Divider>
              <Row gutter={16}>
                <Col span={12}>
                  <Statistic
                    title="Front Threshold"
                    value={deviceStatus.thresholdFront ?? 'N/A'}
                    suffix={deviceStatus.thresholdFront !== null ? 'mm' : ''}
                  />
                </Col>
                <Col span={12}>
                  <Statistic
                    title="Back Threshold"
                    value={deviceStatus.thresholdBack ?? 'N/A'}
                    suffix={deviceStatus.thresholdBack !== null ? 'mm' : ''}
                  />
                </Col>
              </Row>
            </>
          )}

          {/* Live Distance Readings */}
          <Divider>
            <DashboardOutlined /> Live Distance
          </Divider>

          {distanceReading && distanceReading.macAddress === device.macAddress ? (
            <Row gutter={16}>
              <Col span={12}>
                <Statistic
                  title="Front Sensor"
                  value={distanceReading.frontDistance}
                  suffix="mm"
                  styles={{ content: { color: '#1890ff' } }}
                />
              </Col>
              <Col span={12}>
                <Statistic
                  title="Back Sensor"
                  value={distanceReading.backDistance}
                  suffix="mm"
                  styles={{ content: { color: '#1890ff' } }}
                />
              </Col>
            </Row>
          ) : (
            <Text type="secondary">Waiting for readings...</Text>
          )}

          {distanceReading && (
            <div style={{ marginTop: 8 }}>
              <Text type="secondary" style={{ fontSize: '0.8em' }}>
                Last update: {new Date(distanceReading.timestamp).toLocaleTimeString()}
              </Text>
            </div>
          )}
        </>
      )}

      <DeviceFormModal
        open={createModalOpen}
        device={undefined}
        zones={zones}
        loading={createLoading}
        initialValues={{ macAddress: device?.macAddress ?? '' }}
        onSubmit={handleCreateSubmit}
        onCancel={handleCreateCancel}
      />
    </Drawer>
  );
}
