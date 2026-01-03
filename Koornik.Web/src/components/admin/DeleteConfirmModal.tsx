/**
 * Delete confirmation modal utility.
 */

import { Modal } from 'antd';
import { ExclamationCircleOutlined } from '@ant-design/icons';
import type { Device } from '../../types';

interface DeleteConfirmOptions {
  /** Type of entity to delete */
  type: 'zone' | 'device';
  /** Name or identifier of the entity */
  name: string;
  /** For zones: list of devices that reference this zone */
  referencingDevices?: Device[];
  /** Called when deletion is confirmed */
  onConfirm: () => void;
}

/**
 * Shows a delete confirmation modal with optional warnings.
 * For zones, displays a warning if any devices reference the zone.
 */
export function showDeleteConfirm({
  type,
  name,
  referencingDevices = [],
  onConfirm,
}: DeleteConfirmOptions): void {
  const entityLabel = type === 'zone' ? 'zone' : 'device';
  const hasReferences = type === 'zone' && referencingDevices.length > 0;

  let content: React.ReactNode = `Are you sure you want to delete the ${entityLabel} "${name}"? This action cannot be undone.`;

  if (hasReferences) {
    const deviceNames = referencingDevices
      .map((d) => d.name || `Device ${d.id.slice(0, 8)}...`)
      .join(', ');
    
    content = (
      <div>
        <p>Are you sure you want to delete the zone "{name}"? This action cannot be undone.</p>
        <p style={{ color: '#faad14', marginTop: 12 }}>
          <ExclamationCircleOutlined style={{ marginRight: 8 }} />
          <strong>Warning:</strong> The following devices reference this zone and may need to be updated:
        </p>
        <p style={{ marginLeft: 24, fontFamily: 'monospace' }}>{deviceNames}</p>
      </div>
    );
  }

  Modal.confirm({
    title: `Delete ${entityLabel.charAt(0).toUpperCase() + entityLabel.slice(1)}`,
    icon: <ExclamationCircleOutlined />,
    content,
    okText: 'Delete',
    okType: 'danger',
    cancelText: 'Cancel',
    onOk: onConfirm,
  });
}

/**
 * Helper to find devices that reference a specific zone.
 */
export function findDevicesReferencingZone(
  zoneId: string,
  devices: Device[]
): Device[] {
  return devices.filter(
    (device) => device.frontZoneId === zoneId || device.backZoneId === zoneId
  );
}
