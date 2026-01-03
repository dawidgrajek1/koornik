/**
 * Modal dialog for creating and editing devices.
 */

import { useEffect } from 'react';
import { Modal, Form, Input, Select } from 'antd';
import type { Device, Zone, CreateDeviceRequest, UpdateDeviceRequest } from '../../types';

interface DeviceFormValues {
  name: string;
  ipAddress: string;
  macAddress: string;
  frontZoneId: string;
  backZoneId: string;
}

interface DeviceFormModalProps {
  /** Whether the modal is visible */
  open: boolean;
  /** Device to edit (undefined for create mode) */
  device?: Device;
  /** List of zones for zone selectors */
  zones: Zone[];
  /** Loading state for submit button */
  loading?: boolean;
  /** Called when form is submitted */
  onSubmit: (values: CreateDeviceRequest | UpdateDeviceRequest) => void;
  /** Called when modal is cancelled */
  onCancel: () => void;
}

export function DeviceFormModal({
  open,
  device,
  zones,
  loading = false,
  onSubmit,
  onCancel,
}: DeviceFormModalProps) {
  const [form] = Form.useForm<DeviceFormValues>();
  const isEditMode = !!device;

  // Reset form when modal opens/closes or device changes
  useEffect(() => {
    if (open) {
      if (device) {
        form.setFieldsValue({
          name: device.name || '',
          ipAddress: device.ipAddress || '',
          macAddress: device.macAddress || '',
          frontZoneId: device.frontZoneId,
          backZoneId: device.backZoneId,
        });
      } else {
        form.resetFields();
      }
    }
  }, [open, device, form]);

  const handleOk = async () => {
    try {
      const values = await form.validateFields();
      
      if (isEditMode && device) {
        const updateRequest: UpdateDeviceRequest = {
          id: device.id,
          name: values.name || null,
          ipAddress: values.ipAddress || null,
          macAddress: values.macAddress || null,
          frontZoneId: values.frontZoneId,
          backZoneId: values.backZoneId,
        };
        onSubmit(updateRequest);
      } else {
        const createRequest: CreateDeviceRequest = {
          name: values.name || null,
          ipAddress: values.ipAddress || null,
          macAddress: values.macAddress || null,
          frontZoneId: values.frontZoneId,
          backZoneId: values.backZoneId,
        };
        onSubmit(createRequest);
      }
    } catch {
      // Form validation failed
    }
  };

  const zoneOptions = zones.map((zone) => ({
    value: zone.id,
    label: zone.name || `Zone ${zone.id.slice(0, 8)}...`,
  }));

  return (
    <Modal
      title={isEditMode ? 'Edit Device' : 'Create Device'}
      open={open}
      onOk={handleOk}
      onCancel={onCancel}
      confirmLoading={loading}
      destroyOnHidden
    >
      <Form
        form={form}
        layout="vertical"
        initialValues={{ name: '', ipAddress: '', macAddress: '' }}
      >
        <Form.Item
          name="name"
          label="Name"
          rules={[
            { required: true, message: 'Name is required' },
            { max: 100, message: 'Name must be at most 100 characters' },
          ]}
        >
          <Input placeholder="Enter device name" />
        </Form.Item>

        <Form.Item
          name="ipAddress"
          label="IP Address"
          rules={[
            { required: true, message: 'IP address is required' },
            {
              pattern: /^(\d{1,3}\.){3}\d{1,3}$/,
              message: 'Please enter a valid IP address',
            },
          ]}
        >
          <Input placeholder="e.g., 192.168.1.100" />
        </Form.Item>

        <Form.Item
          name="macAddress"
          label="MAC Address"
          rules={[
            { required: true, message: 'MAC address is required' },
            {
              pattern: /^([0-9A-Fa-f]{2}[:-]?){5}([0-9A-Fa-f]{2})$/,
              message: 'Please enter a valid MAC address (e.g., AABBCCDDEEFF or AA:BB:CC:DD:EE:FF)',
            },
          ]}
          normalize={(value: string) => value?.replace(/[:-]/g, '').toUpperCase()}
        >
          <Input placeholder="e.g., AABBCCDDEEFF or AA:BB:CC:DD:EE:FF" />
        </Form.Item>

        <Form.Item
          name="frontZoneId"
          label="Front Zone"
        >
          <Select
            placeholder="Select front zone (optional)"
            options={zoneOptions}
            allowClear
            showSearch
            filterOption={(input, option) =>
              (option?.label ?? '').toLowerCase().includes(input.toLowerCase())
            }
          />
        </Form.Item>

        <Form.Item
          name="backZoneId"
          label="Back Zone"
        >
          <Select
            placeholder="Select back zone (optional)"
            options={zoneOptions}
            allowClear
            showSearch
            filterOption={(input, option) =>
              (option?.label ?? '').toLowerCase().includes(input.toLowerCase())
            }
          />
        </Form.Item>
      </Form>
    </Modal>
  );
}
