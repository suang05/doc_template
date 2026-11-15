'use client';

import React, { useState, useCallback, useRef } from 'react';
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
  ConflictBanner,
  Toast,
  type PillIntent,
} from '@/components/ui';
import { ApiError, extractErrorMessage } from '@/lib/api/client';
import type { UserDto } from '@/lib/api/types';
import { userRoles, type UserRole } from '@/constants/domain';
import { useAuthContext } from '@/contexts/AuthContext';
import { useConflictRecovery } from '@/hooks/useConflictRecovery';
import {
  getManagementUserWithETag,
  getUserWithETag,
  inviteUser,
  removeUser,
  setUserStatus,
  updateUserRole,
} from '../api/users.api';
import { InviteUserRequestSchema } from '../schemas/users.schema';
import { useUsers } from '../hooks/useUsers';

const ROLE_PILL: Record<UserRole, PillIntent> = {
  Admin: 'admin',
  Developer: 'dev',
  Viewer: 'idle',
};

const ROLE_OPTIONS = userRoles.map((r) => ({ value: r, label: r }));

export function UsersView() {
  const { users, loading, error, reload } = useUsers();
  const { isSuperAdmin } = useAuthContext();
  const { conflict, captureConflict, clearConflict } = useConflictRecovery();

  const [isInviteOpen, setIsInviteOpen] = useState(false);
  const [inviteForm, setInviteForm] = useState({
    email: '',
    password: '',
    firstName: '',
    lastName: '',
    role: 'Viewer' as UserRole,
  });
  const [inviteErrors, setInviteErrors] = useState<Record<string, string>>({});
  const [isSubmitting, setIsSubmitting] = useState(false);

  const [roleTarget, setRoleTarget] = useState<UserDto | null>(null);
  const [roleEtag, setRoleEtag] = useState<string | null>(null);
  const [newRole, setNewRole] = useState<UserRole>('Viewer');
  const [isRoleOpen, setIsRoleOpen] = useState(false);

  const [removeTarget, setRemoveTarget] = useState<UserDto | null>(null);
  const [removeEtag, setRemoveEtag] = useState<string | null>(null);
  const [isRemoveOpen, setIsRemoveOpen] = useState(false);

  const [togglingUserId, setTogglingUserId] = useState<string | null>(null);
  const [toast, setToast] = useState<{ type: 'success' | 'error' | 'info'; message: string } | null>(
    null,
  );
  const inviteIdempotencyRef = useRef<string | null>(null);

  const openInviteModal = () => {
    setInviteErrors({});
    inviteIdempotencyRef.current = crypto.randomUUID();
    setIsInviteOpen(true);
  };

  const closeInviteModal = () => {
    setIsInviteOpen(false);
    setInviteErrors({});
    inviteIdempotencyRef.current = null;
  };

  const openRoleModal = async (u: UserDto) => {
    clearConflict();
    setRoleTarget(u);
    setRoleEtag(null);
    setNewRole((userRoles.includes(u.role as UserRole) ? u.role : 'Viewer') as UserRole);
    setIsRoleOpen(true);
    try {
      const latest = await getUserWithETag(u.id);
      setRoleTarget(latest.data);
      setRoleEtag(latest.etag);
      setNewRole((userRoles.includes(latest.data.role as UserRole) ? latest.data.role : 'Viewer') as UserRole);
    } catch (err) {
      setToast({ type: 'error', message: extractErrorMessage(err) });
    }
  };

  const openRemoveModal = async (u: UserDto) => {
    clearConflict();
    setRemoveTarget(u);
    setRemoveEtag(null);
    setIsRemoveOpen(true);
    try {
      const latest = await getUserWithETag(u.id);
      setRemoveTarget(latest.data);
      setRemoveEtag(latest.etag);
    } catch (err) {
      setToast({ type: 'error', message: extractErrorMessage(err) });
    }
  };

  const handleReloadConflict = useCallback(async () => {
    clearConflict();
    await reload();
    if (isRoleOpen && roleTarget) {
      try {
        const latest = await getUserWithETag(roleTarget.id);
        setRoleTarget(latest.data);
        setRoleEtag(latest.etag);
        setNewRole((userRoles.includes(latest.data.role as UserRole) ? latest.data.role : 'Viewer') as UserRole);
      } catch (err) {
        setToast({ type: 'error', message: extractErrorMessage(err) });
      }
    } else if (isRemoveOpen && removeTarget) {
      try {
        const latest = await getUserWithETag(removeTarget.id);
        setRemoveTarget(latest.data);
        setRemoveEtag(latest.etag);
      } catch (err) {
        setToast({ type: 'error', message: extractErrorMessage(err) });
      }
    }
    setToast({ type: 'info', message: 'โหลดรายชื่อผู้ใช้ล่าสุดแล้ว' });
  }, [clearConflict, isRemoveOpen, isRoleOpen, reload, removeTarget, roleTarget]);

  const handleInvite = async (e: React.FormEvent) => {
    e.preventDefault();
    setInviteErrors({});

    const parsed = InviteUserRequestSchema.safeParse(inviteForm);
    if (!parsed.success) {
      const errs: Record<string, string> = {};
      for (const issue of parsed.error.issues) {
        const field = String(issue.path[0] ?? 'general');
        errs[field] = issue.message;
      }
      setInviteErrors(errs);
      return;
    }

    setIsSubmitting(true);
    const attemptKey = inviteIdempotencyRef.current ?? crypto.randomUUID();
    inviteIdempotencyRef.current = attemptKey;

    try {
      await inviteUser(parsed.data, attemptKey);
      closeInviteModal();
      setInviteForm({ email: '', password: '', firstName: '', lastName: '', role: 'Viewer' });
      await reload();
      setToast({ type: 'success', message: 'เพิ่มผู้ใช้เข้าโปรเจกต์สำเร็จ' });
    } catch (err) {
      if (err instanceof ApiError && Object.keys(err.fieldErrors).length > 0) {
        setInviteErrors({ ...err.fieldErrors, general: extractErrorMessage(err) });
      } else {
        setInviteErrors({ general: extractErrorMessage(err, 'เพิ่มผู้ใช้ไม่สำเร็จ') });
      }
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleUpdateRole = async () => {
    if (!roleTarget) return;
    setIsSubmitting(true);
    clearConflict();
    try {
      await updateUserRole(roleTarget.id, { role: newRole }, roleEtag);
      setIsRoleOpen(false);
      setRoleTarget(null);
      setRoleEtag(null);
      await reload();
      setToast({ type: 'success', message: 'อัปเดต Role สำเร็จ' });
    } catch (err) {
      if (captureConflict(err)) return;
      setToast({ type: 'error', message: extractErrorMessage(err, 'เปลี่ยน Role ไม่สำเร็จ') });
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleRemove = async () => {
    if (!removeTarget) return;
    setIsSubmitting(true);
    clearConflict();
    try {
      await removeUser(removeTarget.id, removeEtag);
      setIsRemoveOpen(false);
      setRemoveTarget(null);
      setRemoveEtag(null);
      await reload();
      setToast({ type: 'success', message: 'ลบผู้ใช้ออกจากโปรเจกต์สำเร็จ' });
    } catch (err) {
      if (captureConflict(err)) return;
      setToast({ type: 'error', message: extractErrorMessage(err, 'ลบผู้ใช้ไม่สำเร็จ') });
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleToggleStatus = async (user: UserDto) => {
    if (!isSuperAdmin) return;
    clearConflict();
    setTogglingUserId(user.id);
    try {
      const latest = await getManagementUserWithETag(user.id);
      await setUserStatus(user.id, { isActive: !latest.data.isActive }, latest.etag);
      await reload();
      setToast({
        type: 'success',
        message: `${!latest.data.isActive ? 'เปิดใช้งาน' : 'ระงับบัญชี'} ${user.email} สำเร็จ`,
      });
    } catch (err) {
      if (captureConflict(err)) return;
      setToast({ type: 'error', message: extractErrorMessage(err, 'เปลี่ยนสถานะไม่สำเร็จ') });
    } finally {
      setTogglingUserId(null);
    }
  };

  const columns: Column<UserDto>[] = [
    {
      header: 'ผู้ใช้',
      accessor: (u) => (
        <div className="flex items-center gap-2.5">
          <div className="w-7 h-7 rounded-full bg-primary/10 flex items-center justify-center flex-shrink-0">
            <span className="text-[10px] font-semibold text-primary">
              {((u.firstName?.[0] ?? '') + (u.lastName?.[0] ?? '')).toUpperCase() ||
                u.email[0].toUpperCase()}
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
          {new Date(u.createdAt).toLocaleDateString('th-TH', {
            year: 'numeric',
            month: 'short',
            day: 'numeric',
          })}
        </span>
      ),
    },
    {
      header: '',
      accessor: (u) => (
        <div className="flex items-center gap-1 justify-end">
          {isSuperAdmin && (
            <button
              type="button"
              disabled={togglingUserId === u.id}
              onClick={() => void handleToggleStatus(u)}
              className="p-1 rounded hover:bg-surface2 text-textMuted hover:text-textSecondary transition-colors cursor-pointer disabled:opacity-50"
              aria-label={u.isActive ? `ระงับบัญชี ${u.email}` : `เปิดใช้งาน ${u.email}`}
              title={u.isActive ? 'ระงับบัญชีระดับแพลตฟอร์ม (SuperAdmin)' : 'เปิดใช้งานบัญชี (SuperAdmin)'}
            >
              {u.isActive ? <UserX className="w-3.5 h-3.5" /> : <UserCheck className="w-3.5 h-3.5" />}
            </button>
          )}
          <button
            type="button"
            onClick={() => void openRoleModal(u)}
            className="p-1 rounded hover:bg-surface2 text-textMuted hover:text-textSecondary transition-colors cursor-pointer"
            aria-label={`เปลี่ยน Role ของ ${u.email}`}
            title="เปลี่ยน Role"
          >
            <Shield className="w-3.5 h-3.5" />
          </button>
          <button
            type="button"
            onClick={() => void openRemoveModal(u)}
            className="p-1 rounded hover:bg-surface2 text-textMuted hover:text-red-500 transition-colors cursor-pointer"
            aria-label={`ลบ ${u.email} ออกจากโปรเจกต์`}
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
      {toast && <Toast type={toast.type} message={toast.message} onClose={() => setToast(null)} />}

      <PageHeader
        title="จัดการผู้ใช้"
        description="จัดการสมาชิกและสิทธิ์การเข้าถึงโปรเจกต์"
        icon={Users}
        iconColor="indigo"
        rightSlot={
          <Button icon={UserPlus} onClick={openInviteModal}>
            เพิ่มผู้ใช้
          </Button>
        }
      />

      {!isRoleOpen && !isRemoveOpen && conflict && (
        <ConflictBanner
          conflict={conflict}
          onReload={handleReloadConflict}
          onDismiss={clearConflict}
        />
      )}

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
          onAction={openInviteModal}
        />
      ) : (
        <Table data={users} columns={columns} keyExtractor={(u) => u.id} loading={loading} />
      )}

      <Modal isOpen={isInviteOpen} onClose={closeInviteModal} title="เพิ่มผู้ใช้ใหม่">
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
            error={inviteErrors.role}
            onChange={(e) => setInviteForm((f) => ({ ...f, role: e.target.value as UserRole }))}
          />
          <div className="flex justify-end gap-2 pt-1">
            <Button variant="ghost" size="sm" type="button" onClick={closeInviteModal}>
              ยกเลิก
            </Button>
            <Button size="sm" type="submit" loading={isSubmitting}>
              เพิ่มผู้ใช้
            </Button>
          </div>
        </form>
      </Modal>

      <Modal
        isOpen={isRoleOpen}
        onClose={() => {
          setIsRoleOpen(false);
          setRoleTarget(null);
          setRoleEtag(null);
          clearConflict();
        }}
        title="เปลี่ยน Role"
      >
        <div className="space-y-3">
          {conflict && (
            <ConflictBanner
              conflict={conflict}
              onReload={handleReloadConflict}
              onDismiss={clearConflict}
            />
          )}
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
            <Button
              variant="ghost"
              size="sm"
              onClick={() => {
                setIsRoleOpen(false);
                setRoleTarget(null);
                setRoleEtag(null);
                clearConflict();
              }}
            >
              ยกเลิก
            </Button>
            <Button size="sm" loading={isSubmitting} onClick={() => void handleUpdateRole()}>
              บันทึก
            </Button>
          </div>
        </div>
      </Modal>

      <Modal
        isOpen={isRemoveOpen}
        onClose={() => {
          setIsRemoveOpen(false);
          setRemoveTarget(null);
          setRemoveEtag(null);
          clearConflict();
        }}
        title="ลบผู้ใช้ออกจากโปรเจกต์"
      >
        <div className="space-y-4">
          {conflict && (
            <ConflictBanner
              conflict={conflict}
              onReload={handleReloadConflict}
              onDismiss={clearConflict}
            />
          )}
          <p className="text-xs text-textSecondary">
            คุณต้องการลบ <span className="font-semibold text-textPrimary">{removeTarget?.email}</span>{' '}
            ออกจากโปรเจกต์นี้ใช่หรือไม่?
          </p>
          <div className="flex justify-end gap-2">
            <Button
              variant="ghost"
              size="sm"
              onClick={() => {
                setIsRemoveOpen(false);
                setRemoveTarget(null);
                setRemoveEtag(null);
                clearConflict();
              }}
            >
              ยกเลิก
            </Button>
            <Button
              size="sm"
              variant="danger"
              loading={isSubmitting}
              onClick={() => void handleRemove()}
            >
              ลบออก
            </Button>
          </div>
        </div>
      </Modal>
    </div>
  );
}
