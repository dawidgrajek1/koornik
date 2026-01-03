/**
 * Modal dialog for creating and editing zones.
 */

import { useEffect } from 'react';
import { Modal, Form, Input, InputNumber } from 'antd';
import type { Zone, CreateZoneRequest, UpdateZoneRequest } from '../../types';

interface ZoneFormValues {
  name: string;
  capacity: number;
}

interface ZoneFormModalProps {
  /** Whether the modal is visible */
  open: boolean;
  /** Zone to edit (undefined for create mode) */
  zone?: Zone;
  /** Loading state for submit button */
  loading?: boolean;
  /** Called when form is submitted */
  onSubmit: (values: CreateZoneRequest | UpdateZoneRequest) => void;
  /** Called when modal is cancelled */
  onCancel: () => void;
}

export function ZoneFormModal({
  open,
  zone,
  loading = false,
  onSubmit,
  onCancel,
}: ZoneFormModalProps) {
  const [form] = Form.useForm<ZoneFormValues>();
  const isEditMode = !!zone;

  // Reset form when modal opens/closes or zone changes
  useEffect(() => {
    if (open) {
      if (zone) {
        form.setFieldsValue({
          name: zone.name || '',
          capacity: zone.capacity,
        });
      } else {
        form.resetFields();
      }
    }
  }, [open, zone, form]);

  const handleOk = async () => {
    try {
      const values = await form.validateFields();
      
      if (isEditMode && zone) {
        const updateRequest: UpdateZoneRequest = {
          id: zone.id,
          name: values.name || null,
          capacity: values.capacity,
          geoJson: zone.geoJson, // Preserve existing geoJson
        };
        onSubmit(updateRequest);
      } else {
        const createRequest: CreateZoneRequest = {
          name: values.name || null,
          capacity: values.capacity,
        };
        onSubmit(createRequest);
      }
    } catch {
      // Form validation failed
    }
  };

  return (
    <Modal
      title={isEditMode ? 'Edit Zone' : 'Create Zone'}
      open={open}
      onOk={handleOk}
      onCancel={onCancel}
      confirmLoading={loading}
      destroyOnHidden
    >
      <Form
        form={form}
        layout="vertical"
        initialValues={{ name: '', capacity: 10 }}
      >
        <Form.Item
          name="name"
          label="Name"
          rules={[{ max: 100, message: 'Name must be at most 100 characters' }]}
        >
          <Input placeholder="Enter zone name (optional)" />
        </Form.Item>

        <Form.Item
          name="capacity"
          label="Capacity"
          rules={[
            { required: true, message: 'Capacity is required' },
            { type: 'number', min: 1, message: 'Capacity must be at least 1' },
          ]}
        >
          <InputNumber
            min={1}
            style={{ width: '100%' }}
            placeholder="Enter zone capacity"
          />
        </Form.Item>
      </Form>
    </Modal>
  );
}
