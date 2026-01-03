/**
 * Zones table component displaying zone configuration with drawing capability.
 */

import { Table, Button, Tag, Space, Typography, Tooltip } from 'antd';
import { EditOutlined, CheckCircleOutlined, CloseCircleOutlined, DeleteOutlined } from '@ant-design/icons';
import type { ColumnsType } from 'antd/es/table';
import type { Zone, ZoneState } from '../types';

const { Text } = Typography;

interface ZonesTableProps {
  /** List of zones */
  zones: Zone[];
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
}

export function ZonesTable({
  zones,
  zoneStates,
  selectedZoneId,
  drawingZoneId,
  onZoneSelect,
  onDrawZone,
  onClearZoneGeoJson,
}: ZonesTableProps) {
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
      width: 140,
      fixed: 'right',
      render: (_, zone) => (
        <Space size="small" wrap>
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
                type="primary"
                size="small"
                icon={<DeleteOutlined />}
                danger
                onClick={(e) => {
                  e.stopPropagation();
                  onClearZoneGeoJson(zone.id);
                }}
              />
            </Tooltip>
          )}
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
        {drawingZoneId && (
          <Tag color="processing">
            Drawing mode active
          </Tag>
        )}
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
    </div>
  );
}
