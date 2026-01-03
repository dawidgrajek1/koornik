/**
 * Zones table component displaying zone configuration with drawing capability.
 */

import { useState, useCallback } from 'react';
import { Table, Button, Tag, Space, Typography, Tooltip, App } from 'antd';
import { EditOutlined, CheckCircleOutlined, CloseCircleOutlined, DeleteOutlined, PlusOutlined, SettingOutlined } from '@ant-design/icons';
import type { ColumnsType } from 'antd/es/table';
import type { Zone, ZoneState, Device, CreateZoneRequest, UpdateZoneRequest } from '../types';
import { ZoneFormModal, showDeleteConfirm, findDevicesReferencingZone } from './admin';
import { createZone, updateZone, deleteZone } from '../services/api';

const { Text } = Typography;

interface ZonesTableProps {
  /** List of zones */
  zones: Zone[];
  /** List of devices (for zone reference check on delete) */
  devices: Device[];
  /** Current zone states (occupancy data) */
  zoneStates: Map<string, ZoneState>;
  /** Currently selected zone ID */
  selectedZoneId: string | null;
  /** Zone currently being drawn */
  drawingZoneId: string | null;
  /** Called when a zone row is clicked */
  onZoneSelect: (zoneId: string | null) => void;
  /** Called when draw button is clicked */
  onDrawZone: (zoneId: string) => void;
  /** Called when clear geoJson button is clicked */
  onClearZoneGeoJson: (zoneId: string) => void;
  /** Called when zones data should be refreshed */
  onRefresh: () => void;
}

export function ZonesTable({
  zones,
  devices,
  zoneStates,
  selectedZoneId,
  drawingZoneId,
  onZoneSelect,
  onDrawZone,
  onClearZoneGeoJson,
  onRefresh,
}: ZonesTableProps) {
  const { message } = App.useApp();
  const [modalOpen, setModalOpen] = useState(false);
  const [editingZone, setEditingZone] = useState<Zone | undefined>(undefined);
  const [modalLoading, setModalLoading] = useState(false);

  const handleAddClick = useCallback(() => {
    setEditingZone(undefined);
    setModalOpen(true);
  }, []);

  const handleEditClick = useCallback((zone: Zone) => {
    setEditingZone(zone);
    setModalOpen(true);
  }, []);

  const handleDeleteClick = useCallback((zone: Zone) => {
    const referencingDevices = findDevicesReferencingZone(zone.id, devices);
    showDeleteConfirm({
      type: 'zone',
      name: zone.name || `Zone ${zone.id.slice(0, 8)}...`,
      referencingDevices,
      onConfirm: async () => {
        try {
          await deleteZone(zone.id);
          message.success('Zone deleted successfully');
          onRefresh();
        } catch (error) {
          message.error(`Failed to delete zone: ${error instanceof Error ? error.message : 'Unknown error'}`);
        }
      },
    });
  }, [devices, message, onRefresh]);

  const handleModalSubmit = useCallback(async (values: CreateZoneRequest | UpdateZoneRequest) => {
    setModalLoading(true);
    try {
      if ('id' in values) {
        await updateZone(values);
        message.success('Zone updated successfully');
      } else {
        await createZone(values);
        message.success('Zone created successfully');
      }
      setModalOpen(false);
      onRefresh();
    } catch (error) {
      message.error(`Failed to save zone: ${error instanceof Error ? error.message : 'Unknown error'}`);
    } finally {
      setModalLoading(false);
    }
  }, [message, onRefresh]);

  const handleModalCancel = useCallback(() => {
    setModalOpen(false);
    setEditingZone(undefined);
  }, []);

  const columns: ColumnsType<Zone> = [
    {
      title: 'Name',
      dataIndex: 'name',
      key: 'name',
      render: (name: string | null) => name || <Text type="secondary">Unnamed</Text>,
    },
    {
      title: 'Capacity',
      dataIndex: 'capacity',
      key: 'capacity',
      width: 80,
      align: 'center',
    },
    {
      title: 'Occupancy',
      key: 'occupancy',
      width: 100,
      align: 'center',
      render: (_, zone) => {
        const state = zoneStates.get(zone.id);
        if (!state) return <Text type="secondary">-</Text>;
        
        const percentage = state.occupancyPercentage;
        let color = 'green';
        if (percentage >= 90) color = 'red';
        else if (percentage >= 70) color = 'orange';
        else if (percentage >= 50) color = 'gold';
        
        return (
          <Tag color={color}>
            {state.occupancyCount}/{state.capacity}
          </Tag>
        );
      },
    },
    {
      title: 'On map',
      key: 'geoJson',
      width: 80,
      align: 'center',
      render: (_, zone) => (
        zone.geoJson ? (
          <CheckCircleOutlined style={{ color: '#52c41a' }} />
        ) : (
          <CloseCircleOutlined style={{ color: '#ff4d4f' }} />
        )
      ),
    },
    {
      title: 'Actions',
      key: 'actions',
      width: 180,
      fixed: 'right',
      render: (_, zone) => (
        <Space size="small" wrap>
          <Tooltip title="Edit zone">
            <Button
              size="small"
              icon={<SettingOutlined />}
              onClick={(e) => {
                e.stopPropagation();
                handleEditClick(zone);
              }}
            />
          </Tooltip>
          <Tooltip title={zone.geoJson ? 'Redraw zone boundary' : 'Draw zone boundary'}>
            <Button
              type={drawingZoneId === zone.id ? 'primary' : 'default'}
              size="small"
              icon={<EditOutlined />}
              onClick={(e) => {
                e.stopPropagation();
                onDrawZone(zone.id);
              }}
              disabled={drawingZoneId !== null && drawingZoneId !== zone.id}
            />
          </Tooltip>
          {zone.geoJson && (
            <Tooltip title="Remove from map">
              <Button
                size="small"
                icon={<DeleteOutlined />}
                onClick={(e) => {
                  e.stopPropagation();
                  onClearZoneGeoJson(zone.id);
                }}
              />
            </Tooltip>
          )}
          <Tooltip title="Delete zone">
            <Button
              size="small"
              icon={<DeleteOutlined />}
              danger
              onClick={(e) => {
                e.stopPropagation();
                handleDeleteClick(zone);
              }}
            />
          </Tooltip>
        </Space>
      ),
    },
  ];

  return (
    <div style={{ height: '100%', display: 'flex', flexDirection: 'column', overflow: 'hidden' }}>
      <div
        style={{
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'space-between',
          marginBottom: 12,
          flexShrink: 0,
        }}
      >
        <Text strong style={{ fontSize: 16, color: '#fff' }}>
          Zones
        </Text>
        <Space>
          {drawingZoneId && (
            <Tag color="processing">
              Drawing mode active
            </Tag>
          )}
          <Button
            type="primary"
            size="small"
            icon={<PlusOutlined />}
            onClick={handleAddClick}
          >
            Add Zone
          </Button>
        </Space>
      </div>

      <div style={{ flex: 1, overflow: 'auto', minHeight: 0 }}>
        <Table<Zone>
          columns={columns}
          dataSource={zones}
          rowKey="id"
          size="small"
          pagination={false}
          scroll={{ x: 'max-content' }}
          onRow={(zone) => ({
            onClick: () => onZoneSelect(selectedZoneId === zone.id ? null : zone.id),
            style: {
              cursor: 'pointer',
              background: selectedZoneId === zone.id ? '#177ddc22' : undefined,
            },
          })}
        />
      </div>

      <ZoneFormModal
        open={modalOpen}
        zone={editingZone}
        loading={modalLoading}
        onSubmit={handleModalSubmit}
        onCancel={handleModalCancel}
      />
    </div>
  );
}
