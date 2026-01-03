/**
 * Device panel component displaying list of devices with status.
 */

import { Badge, Typography, Card, Flex, Empty } from 'antd';
import { WifiOutlined, ApiOutlined } from '@ant-design/icons';
import type { Device, DeviceStatusEvent } from '../types';

const { Text } = Typography;

interface DevicePanelProps {
  /** List of devices */
  devices: Device[];
  /** Device status events by MAC address */
  deviceStatuses: Map<string, DeviceStatusEvent>;
  /** Called when a device is clicked */
  onDeviceClick: (device: Device) => void;
}

/** Get badge status based on device state */
function getStatusBadge(status: DeviceStatusEvent | undefined): {
  status: 'success' | 'warning' | 'error' | 'default';
  text: string;
} {
  if (!status) {
    return { status: 'default', text: 'Unknown' };
  }

  const state = status.state.toUpperCase();
  
  switch (state) {
    case 'IDLE':
      return { status: 'success', text: 'Idle' };
    case 'DETECTING':
      return { status: 'warning', text: 'Detecting' };
    case 'ERROR':
      return { status: 'error', text: 'Error' };
    default:
      return { status: 'default', text: state };
  }
}

/** Format last seen timestamp */
function formatLastSeen(timestamp: string | undefined): string {
  if (!timestamp) return 'Never';
  
  const date = new Date(timestamp);
  const now = new Date();
  const diffMs = now.getTime() - date.getTime();
  const diffSec = Math.floor(diffMs / 1000);
  
  if (diffSec < 60) return 'Just now';
  if (diffSec < 3600) return `${Math.floor(diffSec / 60)}m ago`;
  if (diffSec < 86400) return `${Math.floor(diffSec / 3600)}h ago`;
  return date.toLocaleDateString();
}

export function DevicePanel({
  devices,
  deviceStatuses,
  onDeviceClick,
}: DevicePanelProps) {
  return (
    <div style={{ height: '100%', overflow: 'auto' }}>
      <div style={{ padding: '12px 16px', borderBottom: '1px solid #303030' }}>
        <Text strong style={{ fontSize: '1rem' }}>
          <ApiOutlined style={{ marginRight: 8 }} />
          Devices ({devices.length})
        </Text>
      </div>

      {devices.length === 0 ? (
        <Empty description="No devices found" style={{ padding: '24px' }} />
      ) : (
        <Flex vertical>
          {devices.map(device => {
            const status = device.macAddress
              ? deviceStatuses.get(device.macAddress)
              : undefined;
            const badgeInfo = getStatusBadge(status);

            return (
              <div
                key={device.id}
                style={{ padding: '8px 16px', cursor: 'pointer' }}
                onClick={() => onDeviceClick(device)}
              >
                <Card
                  size="small"
                  style={{ width: '100%' }}
                  hoverable
                  styles={{ body: { padding: '12px' } }}
                >
                  <div
                    style={{
                      display: 'flex',
                      justifyContent: 'space-between',
                      alignItems: 'flex-start',
                    }}
                  >
                    <div>
                      <div style={{ marginBottom: 4 }}>
                        <WifiOutlined style={{ marginRight: 6 }} />
                        <Text strong>{device.name || 'Unnamed Device'}</Text>
                      </div>
                      <Text
                        type="secondary"
                        style={{ fontSize: '0.8em', fontFamily: 'monospace' }}
                      >
                        {device.macAddress || 'No MAC'}
                      </Text>
                    </div>
                    <div style={{ textAlign: 'right' }}>
                      <Badge status={badgeInfo.status} text={badgeInfo.text} />
                      <div>
                        <Text
                          type="secondary"
                          style={{ fontSize: '0.75em' }}
                        >
                          {formatLastSeen(status?.timestamp)}
                        </Text>
                      </div>
                    </div>
                  </div>
                </Card>
              </div>
            );
          })}
        </Flex>
      )}
    </div>
  );
}
