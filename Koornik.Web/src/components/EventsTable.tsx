/**
 * Events table component displaying real-time transition events.
 * Loads today's transitions on mount and updates with real-time events.
 * Supports filtering by zone.
 */

import { useMemo, useEffect, useState, useCallback } from 'react';
import { Table, Tag, Button, Space, Typography } from 'antd';
import { CloseCircleOutlined, ArrowRightOutlined, ReloadOutlined } from '@ant-design/icons';
import type { ColumnsType } from 'antd/es/table';
import type { TransitionEvent } from '../types';
import type { Zone } from '../types';
import type { Device } from '../types';
import { getTransitionsByTimeRange, getDevices } from '../services/api';

const { Text } = Typography;

/** Get start of today as ISO string */
function getStartOfToday(): string {
  const today = new Date();
  today.setHours(0, 0, 0, 0);
  return today.toISOString();
}

/** Get end of today as ISO string */
function getEndOfToday(): string {
  const today = new Date();
  today.setHours(23, 59, 59, 999);
  return today.toISOString();
}

interface EventsTableProps {
  /** Real-time transition events from SignalR */
  transitions: TransitionEvent[];
  /** Zones for name lookup */
  zones: Zone[];
  /** Devices for name lookup (used when loading historical data) */
  devices?: Device[];
  /** Zone ID to filter by (null = show all) */
  filterZoneId: string | null;
  /** Called to clear the zone filter */
  onClearFilter: () => void;
}

export function EventsTable({
  transitions,
  zones,
  devices: propDevices,
  filterZoneId,
  onClearFilter,
}: EventsTableProps) {
  const [historicalTransitions, setHistoricalTransitions] = useState<TransitionEvent[]>([]);
  const [loading, setLoading] = useState(true);
  const [deviceMap, setDeviceMap] = useState<Map<string, Device>>(new Map());

  // Load devices for name lookup if not provided via props
  useEffect(() => {
    if (propDevices && propDevices.length > 0) {
      const map = new Map<string, Device>();
      propDevices.forEach(device => {
        map.set(device.id, device);
      });
      setDeviceMap(map);
    } else {
      getDevices()
        .then(devices => {
          const map = new Map<string, Device>();
          devices.forEach(device => {
            map.set(device.id, device);
          });
          setDeviceMap(map);
        })
        .catch(err => console.error('Failed to load devices:', err));
    }
  }, [propDevices]);

  // Load today's transitions on mount
  const loadTodaysTransitions = useCallback(async () => {
    try {
      setLoading(true);
      const startOfToday = getStartOfToday();
      const endOfToday = getEndOfToday();
      const apiTransitions = await getTransitionsByTimeRange(startOfToday, endOfToday);
      
      // Convert API transitions to TransitionEvent format
      const events: TransitionEvent[] = apiTransitions.map(t => {
        const device = deviceMap.get(t.deviceId);
        return {
          transitionId: t.id,
          deviceId: t.deviceId,
          deviceName: device?.name || '',
          macAddress: device?.macAddress || '',
          direction: 'IN' as const, // API doesn't provide direction, default to IN
          fromZoneId: t.fromZoneId,
          toZoneId: t.toZoneId,
          timestamp: t.createdAt,
        };
      });
      
      // Sort by timestamp descending (newest first)
      events.sort((a, b) => new Date(b.timestamp).getTime() - new Date(a.timestamp).getTime());
      setHistoricalTransitions(events);
    } catch (err) {
      console.error('Failed to load today\'s transitions:', err);
    } finally {
      setLoading(false);
    }
  }, [deviceMap]);

  // Load transitions when device map is ready
  useEffect(() => {
    if (deviceMap.size > 0 || (propDevices && propDevices.length === 0)) {
      loadTodaysTransitions();
    }
  }, [deviceMap, loadTodaysTransitions, propDevices]);

  // Merge real-time transitions with historical, removing duplicates
  const allTransitions = useMemo(() => {
    const seen = new Set<string>();
    const merged: TransitionEvent[] = [];
    
    // Real-time transitions take priority (they have complete data)
    for (const t of transitions) {
      if (!seen.has(t.transitionId)) {
        seen.add(t.transitionId);
        merged.push(t);
      }
    }
    
    // Add historical transitions that aren't already in real-time
    for (const t of historicalTransitions) {
      if (!seen.has(t.transitionId)) {
        seen.add(t.transitionId);
        merged.push(t);
      }
    }
    
    // Sort by timestamp descending (newest first)
    merged.sort((a, b) => new Date(b.timestamp).getTime() - new Date(a.timestamp).getTime());
    
    return merged;
  }, [transitions, historicalTransitions]);
  // Create zone name lookup map
  const zoneNameMap = useMemo(() => {
    const map = new Map<string, string>();
    zones.forEach(zone => {
      map.set(zone.id, zone.name || 'Unknown');
    });
    return map;
  }, [zones]);

  // Filter transitions if a zone is selected
  const filteredTransitions = useMemo(() => {
    if (!filterZoneId) return allTransitions;
    return allTransitions.filter(
      t => t.fromZoneId === filterZoneId || t.toZoneId === filterZoneId
    );
  }, [allTransitions, filterZoneId]);

  const columns: ColumnsType<TransitionEvent> = [
    {
      title: 'Time',
      dataIndex: 'timestamp',
      key: 'timestamp',
      width: 180,
      render: (timestamp: string) => {
        const date = new Date(timestamp);
        return (
          <Text style={{ fontSize: '0.85em' }}>
            {date.toLocaleTimeString()} <br />
            <Text type="secondary" style={{ fontSize: '0.9em' }}>
              {date.toLocaleDateString()}
            </Text>
          </Text>
        );
      },
    },
    {
      title: 'Device',
      dataIndex: 'deviceName',
      key: 'deviceName',
      width: 150,
      ellipsis: true,
      render: (name: string, record) => (
        <span title={record.macAddress}>{name || record.macAddress}</span>
      ),
    },
    {
      title: 'Direction',
      dataIndex: 'direction',
      key: 'direction',
      width: 80,
      align: 'center',
      render: (direction: 'IN' | 'OUT') => (
        <Tag color={direction === 'IN' ? 'green' : 'red'}>
          {direction}
        </Tag>
      ),
    },
    {
      title: 'Transition',
      key: 'transition',
      render: (_, record) => {
        const fromName = zoneNameMap.get(record.fromZoneId) || 'Unknown';
        const toName = zoneNameMap.get(record.toZoneId) || 'Unknown';
        return (
          <Space size="small">
            <Text>{fromName}</Text>
            <ArrowRightOutlined style={{ color: '#888' }} />
            <Text>{toName}</Text>
          </Space>
        );
      },
    },
  ];

  const filterZoneName = filterZoneId ? zoneNameMap.get(filterZoneId) : null;

  return (
    <div style={{ height: '100%', display: 'flex', flexDirection: 'column' }}>
      {/* Header with filter indicator and refresh button */}
      <div
        style={{
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
          padding: '8px 0',
        }}
      >
        <Space>
          <Text strong>Today's Events</Text>
          <Button
            size="small"
            type="text"
            icon={<ReloadOutlined spin={loading} />}
            onClick={loadTodaysTransitions}
            disabled={loading}
            title="Refresh"
          />
        </Space>
        {filterZoneId && (
          <Button
            size="small"
            type="text"
            icon={<CloseCircleOutlined />}
            onClick={onClearFilter}
          >
            Filtering by: {filterZoneName}
          </Button>
        )}
      </div>

      {/* Events table */}
      <Table
        dataSource={filteredTransitions}
        columns={columns}
        rowKey="transitionId"
        size="small"
        loading={loading}
        pagination={{ pageSize: 10, showSizeChanger: false }}
        scroll={{ y: 300 }}
        locale={{
          emptyText: filterZoneId
            ? 'No events for this zone today'
            : 'No events today',
        }}
      />
    </div>
  );
}
