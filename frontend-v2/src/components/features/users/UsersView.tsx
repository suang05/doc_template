'use client';

import React, { useState } from 'react';
import { UserPlus, Trash2, Shield, UserCheck, UserX, Users } from 'lucide-react';
import {
  Button,
  Badge,
  Input,
  Select,
  Table,
  Column,
  Modal,
  EmptyState,
  PageHeader,
  Pill,
  type PillIntent,
} from '@/components/ui';
import { inviteUser, updateUserRole, removeUser, setUserStatus } from '@/lib/api/users.api';
import { InviteUserRequestSchema, userRoles, type UserListItem, type UserRole } from '@/types/api';
import { useUsers } from '@/hooks/useUsers';

const ROLE_PILL: Record<UserRole, PillIntent> = {
  Admin: 'admin',
  Editor: 'dev',
  Viewer: 'idle',
};

const ROLE_OPTIONS = userRoles.map((r) => ({ value: r, label: r }));

export function UsersView() {
  const { users, loading, error, reload } = useUsers();

  const [isInviteOpen, setIsInviteOpen] = useState(false);
  const [inviteForm, setInviteForm] = useState({
    email: '', password: '', firstName: '', lastName: '', role: 'Viewer' as UserRole,
  });
  const [inviteErrors, setInviteErrors] = useState<Record<string, string>>({});
  const [isSubmitting, setIsSubmitting] = useState(false);

  const [roleTarget, setRoleTarget] = useState<UserListItem | null>(null);
  const [newRole, setNewRole] = useState<UserRole>('Viewer');
  const [isRoleOpen, setIsRoleOpen] = useState(false);

  const [removeTarget, setRemoveTarget] = useState<UserListItem | null>(null);
  const [isRemoveOpen, setIsRemoveOpen] = useState(false);

  const handleInvite = async (e: React.FormEvent) => {
    e.preventDefault();
    setInviteErrors({});

    const parsed = InviteUserRequestSchema.safeParse(inviteForm);
    if (!parsed.success) {
      const errs: Record<string, string> = {};
      for (const issue of parsed.error.issues) {
        const field = issue.path[0] as string;
        errs[field] = issue.message;
      }
      setInviteErrors(errs);
      return;
    }

    setIsSubmitting(true);
    try {
      await inviteUser(parsed.data);
      setIsInviteOpen(false);
      setInviteForm({ email: '', password: '', firstName: '', lastName: '', role: 'Viewer' });
      await reload();
    } catch (err: unknown) {
      setInviteErrors({ general: err instanceof Error ? err.message : 'เพิ่มผู้ใช้ไม่สำเร็จ' });
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleUpdateRole = async () => {
    if (!roleTarget) return;
    setIsSubmitting(true);
    try {
      await updateUserRole(roleTarget.id, { role: newRole });
      setIsRoleOpen(false);
      setRoleTarget(null);
      await reload();
    } catch (err: unknown) {
      alert(err instanceof Error ? err.message : 'เปลี่ยน Role ไม่สำเร็จ');
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleRemove = async () => {
    if (!removeTarget) return;
    setIsSubmitting(true);
    try {
      await removeUser(removeTarget.id);
      setIsRemoveOpen(false);
      setRemoveTarget(null);
      await reload();
    } catch (err: unknown) {
      alert(err instanceof Error ? err.message : 'ลบผู้ใช้ไม่สำเร็จ');
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleToggleStatus = async (user: UserListItem) => {
    try {
      await setUserStatus(user.id, !user.isActive);
      await reload();
    } catch (err: unknown) {
      alert(err instanceof Error ? err.message : 'เปลี่ยนสถานะไม่สำเร็จ');
    }
  };

  const columns: Column<UserListItem>[] = [
    {
      header: 'ผู้ใช้',
      accessor: (u) => (
        <div className="flex items-center gap-2.5">
          <div className="w-7 h-7 rounded-full bg-primary/10 flex items-center justify-center flex-shrink-0">
            <span className="text-[10px] font-semibold text-primary">
              {((u.firstName?.[0] ?? '') + (u.lastName?.[0] ?? '')).toUpperCase() || u.email[0].toUpperCase()}
            </span>
          </div>
          <div className="min-w-0">
            <p className="text-xs font-semibold text-textPrimary truncate">
              {u.firstName || u.lastName ? `${u.firstName} ${u.lastName}`.trim() : '—'}
            </p>
            <p className="text-[10px] text-textMuted truncate">{u.email}</p>
          </div>
        </div>
      ),
    },
    {
      header: 'Role',
      accessor: (u) => (
        <Pill intent={ROLE_PILL[u.role as UserRole] ?? 'idle'} size="sm">
          {u.role}
        </Pill>
      ),
    },
    {
      header: 'สถานะ',
      accessor: (u) => (
        <Badge status={u.isActive ? 'success' : 'idle'} dot size="sm">
          {u.isActive ? 'เปิดใช้งาน' : 'ระงับแล้ว'}
        </Badge>
      ),
    },
    {
      header: 'เพิ่มเมื่อ',
      accessor: (u) => (
        <span className="text-xs text-textMuted">
          {new Date(u.createdAt).toLocaleDateString('th-TH', { year: 'numeric', month: 'short', day: 'numeric' })}
        </span>
      ),
    },
    {
      header: '',
      accessor: (u) => (
        <div className="flex items-center gap-1 justify-end">
          <button
            onClick={() => handleToggleStatus(u)}
            className="p-1 rounded hover:bg-surface2 text-textMuted hover:text-textSecondary transition-colors"
            title={u.isActive ? 'ระงับบัญชี' : 'เปิดใช้งาน'}
          >
            {u.isActive ? <UserX className="w-3.5 h-3.5" /> : <UserCheck className="w-3.5 h-3.5" />}
          </button>
          <button
            onClick={() => { setRoleTarget(u); setNewRole(u.role as UserRole); setIsRoleOpen(true); }}
            className="p-1 rounded hover:bg-surface2 text-textMuted hover:text-textSecondary transition-colors"
            title="เปลี่ยน Role"
          >
            <Shield className="w-3.5 h-3.5" />
          </button>
          <button
            onClick={() => { setRemoveTarget(u); setIsRemoveOpen(true); }}
            className="p-1 rounded hover:bg-surface2 text-textMuted hover:text-red-500 transition-colors"
            title="ลบออกจากโปรเจกต์"
          >
            <Trash2 className="w-3.5 h-3.5" />
          </button>
        </div>
      ),
    },
  ];

  return (
    <div className="space-y-4 pb-6">
      <PageHeader
        title="จัดการผู้ใช้"
        description="จัดการสมาชิกและสิทธิ์การเข้าถึงโปรเจกต์"
        icon={Users}
        iconColor="indigo"
        rightSlot={
          <Button icon={UserPlus} onClick={() => setIsInviteOpen(true)}>
            เพิ่มผู้ใช้
          </Button>
        }
      />

      {error && (
        <div className="bg-red-50 border border-red-200 rounded-sm px-3 py-2">
          <p className="text-xs text-red-700">{error}</p>
        </div>
      )}

      {!loading && users.length === 0 ? (
        <EmptyState
          icon={UserPlus}
          title="ยังไม่มีผู้ใช้"
          description="เพิ่มผู้ใช้คนแรกเพื่อเริ่มต้นใช้งานร่วมกัน"
          actionLabel="เพิ่มผู้ใช้"
          onAction={() => setIsInviteOpen(true)}
        />
      ) : (
        <Table
          data={users}
          columns={columns}
          keyExtractor={(u) => u.id}
          loading={loading}
        />
      )}

      {/* Invite Modal */}
      <Modal
        isOpen={isInviteOpen}
        onClose={() => { setIsInviteOpen(false); setInviteErrors({}); }}
        title="เพิ่มผู้ใช้ใหม่"
      >
        <form onSubmit={handleInvite} className="space-y-3">
          {inviteErrors.general && (
            <div className="bg-red-50 border border-red-200 rounded-sm px-3 py-2">
              <p className="text-xs text-red-700">{inviteErrors.general}</p>
            </div>
          )}
          <div className="grid grid-cols-2 gap-3">
            <Input
              label="ชื่อ"
              value={inviteForm.firstName}
              onChange={(e) => setInviteForm((f) => ({ ...f, firstName: e.target.value }))}
              error={inviteErrors.firstName}
            />
            <Input
              label="นามสกุล"
              value={inviteForm.lastName}
              onChange={(e) => setInviteForm((f) => ({ ...f, lastName: e.target.value }))}
              error={inviteErrors.lastName}
            />
          </div>
          <Input
            label="อีเมล"
            type="email"
            value={inviteForm.email}
            onChange={(e) => setInviteForm((f) => ({ ...f, email: e.target.value }))}
            error={inviteErrors.email}
          />
          <Input
            label="รหัสผ่าน"
            type="password"
            value={inviteForm.password}
            onChange={(e) => setInviteForm((f) => ({ ...f, password: e.target.value }))}
            error={inviteErrors.password}
          />
          <Select
            label="Role"
            value={inviteForm.role}
            options={ROLE_OPTIONS}
            onChange={(e) => setInviteForm((f) => ({ ...f, role: e.target.value as UserRole }))}
          />
          <div className="flex justify-end gap-2 pt-1">
            <Button
              variant="ghost"
              size="sm"
              type="button"
              onClick={() => { setIsInviteOpen(false); setInviteErrors({}); }}
            >
              ยกเลิก
            </Button>
            <Button size="sm" type="submit" loading={isSubmitting}>
              เพิ่มผู้ใช้
            </Button>
          </div>
        </form>
      </Modal>

      {/* Change Role Modal */}
      <Modal
        isOpen={isRoleOpen}
        onClose={() => { setIsRoleOpen(false); setRoleTarget(null); }}
        title="เปลี่ยน Role"
      >
        <div className="space-y-3">
          <p className="text-xs text-textSecondary">
            เปลี่ยน Role ของ <span className="font-semibold text-textPrimary">{roleTarget?.email}</span>
          </p>
          <Select
            label="Role ใหม่"
            value={newRole}
            options={ROLE_OPTIONS}
            onChange={(e) => setNewRole(e.target.value as UserRole)}
          />
          <div className="flex justify-end gap-2 pt-1">
            <Button variant="ghost" size="sm" onClick={() => { setIsRoleOpen(false); setRoleTarget(null); }}>
              ยกเลิก
            </Button>
            <Button size="sm" loading={isSubmitting} onClick={handleUpdateRole}>
              บันทึก
            </Button>
          </div>
        </div>
      </Modal>

      {/* Remove Confirm Modal */}
      <Modal
        isOpen={isRemoveOpen}
        onClose={() => { setIsRemoveOpen(false); setRemoveTarget(null); }}
        title="ลบผู้ใช้ออกจากโปรเจกต์"
      >
        <div className="space-y-4">
          <p className="text-xs text-textSecondary">
            คุณต้องการลบ <span className="font-semibold text-textPrimary">{removeTarget?.email}</span>{' '}
            ออกจากโปรเจกต์นี้ใช่หรือไม่?
          </p>
          <div className="flex justify-end gap-2">
            <Button variant="ghost" size="sm" onClick={() => { setIsRemoveOpen(false); setRemoveTarget(null); }}>
              ยกเลิก
            </Button>
            <Button size="sm" variant="danger" loading={isSubmitting} onClick={handleRemove}>
              ลบออก
            </Button>
          </div>
        </div>
      </Modal>
    </div>
  );
}
