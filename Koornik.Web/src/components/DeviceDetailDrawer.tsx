/**
 * Device detail drawer component showing device info and live distance readings.
 */

import { useEffect, useMemo } from 'react';
import { Drawer, Descriptions, Badge, Statistic, Row, Col, Typography, Divider, Card } from 'antd';
import { WifiOutlined, DashboardOutlined, EnvironmentOutlined } from '@ant-design/icons';
import type { Device, Zone, ZoneState, DeviceStatusEvent, DistanceReadingEvent } from '../types';

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
}: DeviceDetailDrawerProps) {
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

  // Get zone info
  const frontZone = device ? zones.find(z => z.id === device.frontZoneId) : null;
  const backZone = device ? zones.find(z => z.id === device.backZoneId) : null;
  const frontZoneState = device ? zoneStates.get(device.frontZoneId) : null;
  const backZoneState = device ? zoneStates.get(device.backZoneId) : null;

  // Get status info
  const statusText = deviceStatus?.state || 'Unknown';
  const statusColor =
    statusText.toUpperCase() === 'IDLE'
      ? 'success'
      : statusText.toUpperCase() === 'DETECTING'
      ? 'warning'
      : statusText.toUpperCase() === 'ERROR'
      ? 'error'
      : 'default';

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
              <Badge status={statusColor as 'success' | 'warning' | 'error' | 'default'} text={statusText} />
            </Descriptions.Item>
          </Descriptions>

          <Divider>
            <EnvironmentOutlined /> Connected Zones
          </Divider>

          {/* Front Zone */}
          <Card size="small" style={{ marginBottom: 12 }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
              <div>
                <Text type="secondary">Front Zone</Text>
                <div>
                  <Text strong>{frontZone?.name || zoneNameMap.get(device.frontZoneId) || 'Unknown'}</Text>
                </div>
              </div>
              {frontZoneState && (
                <Statistic
                  title="Occupancy"
                  value={frontZoneState.occupancyCount}
                  suffix={`/ ${frontZoneState.capacity}`}
                  valueStyle={{ fontSize: '1rem' }}
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
                  <Text strong>{backZone?.name || zoneNameMap.get(device.backZoneId) || 'Unknown'}</Text>
                </div>
              </div>
              {backZoneState && (
                <Statistic
                  title="Occupancy"
                  value={backZoneState.occupancyCount}
                  suffix={`/ ${backZoneState.capacity}`}
                  valueStyle={{ fontSize: '1rem' }}
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
                  valueStyle={{ color: '#1890ff' }}
                />
              </Col>
              <Col span={12}>
                <Statistic
                  title="Back Sensor"
                  value={distanceReading.backDistance}
                  suffix="mm"
                  valueStyle={{ color: '#1890ff' }}
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
    </Drawer>
  );
}
