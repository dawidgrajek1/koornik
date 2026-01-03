/**
 * Device panel component displaying list of devices with status.
 */

import { useState, useCallback } from 'react';
import { Badge, Typography, Card, Flex, Empty, Button, Space, App, Tooltip } from 'antd';
import { WifiOutlined, ApiOutlined, PlusOutlined, EditOutlined, DeleteOutlined } from '@ant-design/icons';
import type { Device, Zone, DeviceStatusEvent, CreateDeviceRequest, UpdateDeviceRequest } from '../types';
import { DeviceFormModal, showDeleteConfirm } from './admin';
import { createDevice, updateDevice, deleteDevice } from '../services/api';

const { Text } = Typography;

interface DevicePanelProps {
  /** List of devices */
  devices: Device[];
  /** List of zones (for device form) */
  zones: Zone[];
  /** Device status events by MAC address */
  deviceStatuses: Map<string, DeviceStatusEvent>;
  /** Called when a device is clicked */
  onDeviceClick: (device: Device) => void;
  /** Called when devices data should be refreshed */
  onRefresh: () => void;
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
  zones,
  deviceStatuses,
  onDeviceClick,
  onRefresh,
}: DevicePanelProps) {
  const { message } = App.useApp();
  const [modalOpen, setModalOpen] = useState(false);
  const [editingDevice, setEditingDevice] = useState<Device | undefined>(undefined);
  const [modalLoading, setModalLoading] = useState(false);

  const handleAddClick = useCallback(() => {
    setEditingDevice(undefined);
    setModalOpen(true);
  }, []);

  const handleEditClick = useCallback((device: Device, e: React.MouseEvent) => {
    e.stopPropagation();
    setEditingDevice(device);
    setModalOpen(true);
  }, []);

  const handleDeleteClick = useCallback((device: Device, e: React.MouseEvent) => {
    e.stopPropagation();
    showDeleteConfirm({
      type: 'device',
      name: device.name || `Device ${device.id.slice(0, 8)}...`,
      onConfirm: async () => {
        try {
          await deleteDevice(device.id);
          message.success('Device deleted successfully');
          onRefresh();
        } catch (error) {
          message.error(`Failed to delete device: ${error instanceof Error ? error.message : 'Unknown error'}`);
        }
      },
    });
  }, [message, onRefresh]);

  const handleModalSubmit = useCallback(async (values: CreateDeviceRequest | UpdateDeviceRequest) => {
    setModalLoading(true);
    try {
      if ('id' in values) {
        await updateDevice(values);
        message.success('Device updated successfully');
      } else {
        await createDevice(values);
        message.success('Device created successfully');
      }
      setModalOpen(false);
      onRefresh();
    } catch (error) {
      message.error(`Failed to save device: ${error instanceof Error ? error.message : 'Unknown error'}`);
    } finally {
      setModalLoading(false);
    }
  }, [message, onRefresh]);

  const handleModalCancel = useCallback(() => {
    setModalOpen(false);
    setEditingDevice(undefined);
  }, []);

  return (
    <div style={{ height: '100%', overflow: 'auto' }}>
      <div style={{ padding: '12px 16px', borderBottom: '1px solid #303030', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <Text strong style={{ fontSize: '1rem' }}>
          <ApiOutlined style={{ marginRight: 8 }} />
          Devices ({devices.length})
        </Text>
        <Button
          type="primary"
          size="small"
          icon={<PlusOutlined />}
          onClick={handleAddClick}
        >
          Add
        </Button>
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
                      <div style={{ marginBottom: 4 }}>
                        <Badge status={badgeInfo.status} text={badgeInfo.text} />
                      </div>
                      <div>
                        <Text
                          type="secondary"
                          style={{ fontSize: '0.75em' }}
                        >
                          {formatLastSeen(status?.timestamp)}
                        </Text>
                      </div>
                      <Space size="small" style={{ marginTop: 8 }}>
                        <Tooltip title="Edit device">
                          <Button
                            size="small"
                            icon={<EditOutlined />}
                            onClick={(e) => handleEditClick(device, e)}
                          />
                        </Tooltip>
                        <Tooltip title="Delete device">
                          <Button
                            size="small"
                            icon={<DeleteOutlined />}
                            danger
                            onClick={(e) => handleDeleteClick(device, e)}
                          />
                        </Tooltip>
                      </Space>
                    </div>
                  </div>
                </Card>
              </div>
            );
          })}
        </Flex>
      )}

      <DeviceFormModal
        open={modalOpen}
        device={editingDevice}
        zones={zones}
        loading={modalLoading}
        onSubmit={handleModalSubmit}
        onCancel={handleModalCancel}
      />
    </div>
  );
}
