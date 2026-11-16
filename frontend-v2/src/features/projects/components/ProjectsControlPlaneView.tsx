'use client';

import React, { useState, useRef, useCallback } from 'react';
import { Building2 } from 'lucide-react';
import { ConflictBanner, PageHeader, Tabs, Toast } from '@/components/ui';
import { useConflictRecovery } from '@/hooks/useConflictRecovery';
import { useProjectsControlPlane } from '../hooks/useProjectsControlPlane';
import { TenantsTabSection } from './TenantsTabSection';
import { QuotasTabSection } from './QuotasTabSection';
import { BreakGlassTabSection } from './BreakGlassTabSection';
import { PlatformFontsTabSection } from './PlatformFontsTabSection';

type ControlPlaneTab = 'tenants' | 'quotas' | 'break-glass' | 'fonts';

export const ProjectsControlPlaneView: React.FC = () => {
  const [activeTab, setActiveTab] = useState<ControlPlaneTab>('tenants');
  const [toast, setToast] = useState<{
    type: 'success' | 'error' | 'info';
    message: string;
  } | null>(null);

  const { conflict, captureConflict, clearConflict } = useConflictRecovery();
  const pendingReloadRef = useRef<(() => Promise<void>) | null>(null);

  const {
    tenants,
    search,
    setSearch,
    pagination,
    setPage,
    loadingTenants,
    tenantsError,
    egressCidrsByProjectId,
    mutatingProjectId,
    fetchTenants,
    createProject,
    fetchProjectForEdit,
    toggleProjectActive,
    updateEgressCidrs,
    transferOwner,
    quotaProjectId,
    setQuotaProjectId,
    quota,
    quotaEtag,
    loadingQuota,
    quotaError,
    savingQuota,
    loadQuota,
    saveQuota,
    grants,
    grantFilterProjectId,
    setGrantFilterProjectId,
    loadingGrants,
    grantsError,
    revokingGrantId,
    loadGrants,
    issueGrant,
    revokeGrant,
    fonts,
    loadingFonts,
    fontsError,
    uploadingFont,
    loadFonts,
    uploadFont,
  } = useProjectsControlPlane();

  const notify = useCallback((type: 'success' | 'error' | 'info', message: string) => {
    setToast({ type, message });
  }, []);

  const handleConflict = useCallback(
    (err: unknown, reloadFn?: () => Promise<void>): boolean => {
      if (!captureConflict(err)) return false;
      pendingReloadRef.current = reloadFn ?? (() => fetchTenants());
      return true;
    },
    [captureConflict, fetchTenants],
  );

  const handleReloadConflict = useCallback(async () => {
    const reload = pendingReloadRef.current;
    clearConflict();
    pendingReloadRef.current = null;
    if (reload) {
      await reload();
    } else {
      await fetchTenants();
    }
    notify('info', 'โหลดข้อมูลและ ETag ล่าสุดจากเซิร์ฟเวอร์เรียบร้อยแล้ว');
  }, [clearConflict, fetchTenants, notify]);

  const handleOpenQuotaForProject = useCallback(
    (projectId: string) => {
      setQuotaProjectId(projectId);
      setActiveTab('quotas');
    },
    [setQuotaProjectId],
  );

  return (
    <div className="space-y-4">
      {toast && (
        <Toast
          type={toast.type}
          message={toast.message}
          onClose={() => setToast(null)}
        />
      )}

      <PageHeader
        title="Control Plane & จัดการเทแนนต์ (Projects Governance)"
        description="สร้างเทแนนต์/โปรเจกต์ ควบคุมสถานะและ Outbound Egress CIDRs กำหนดโควตา ออกสิทธิ์ Break-Glass (ZSA) และลงทะเบียน Platform Fonts (SuperAdmin เท่านั้น)"
        icon={Building2}
        iconColor="indigo"
      />

      {conflict && (
        <ConflictBanner
          conflict={conflict}
          onReload={handleReloadConflict}
          onDismiss={clearConflict}
        />
      )}

      <div className="bg-surface border border-border rounded-sm overflow-hidden">
        <Tabs
          activeTab={activeTab}
          onChange={(id) => {
            clearConflict();
            setActiveTab(id as ControlPlaneTab);
          }}
          tabs={[
            { id: 'tenants', label: 'เทแนนต์และโปรเจกต์', count: pagination.totalCount },
            { id: 'quotas', label: 'โควตาการใช้งาน' },
            { id: 'break-glass', label: 'Break-Glass Access', count: grants.length },
            { id: 'fonts', label: 'Platform Fonts', count: fonts.length },
          ]}
        />

        <div className="p-4">
          {activeTab === 'tenants' && (
            <TenantsTabSection
              tenants={tenants}
              search={search}
              onSearchChange={setSearch}
              pagination={pagination}
              onPageChange={setPage}
              loading={loadingTenants}
              error={tenantsError}
              mutatingProjectId={mutatingProjectId}
              egressCidrsByProjectId={egressCidrsByProjectId}
              onRefresh={() => fetchTenants()}
              onCreateProject={createProject}
              onFetchProjectWithETag={fetchProjectForEdit}
              onToggleActive={toggleProjectActive}
              onUpdateEgressCidrs={updateEgressCidrs}
              onTransferOwner={transferOwner}
              onOpenQuotaTab={handleOpenQuotaForProject}
              onConflict={handleConflict}
              onNotify={notify}
            />
          )}

          {activeTab === 'quotas' && (
            <QuotasTabSection
              tenants={tenants}
              selectedProjectId={quotaProjectId}
              onSelectProject={setQuotaProjectId}
              quota={quota}
              quotaEtag={quotaEtag}
              loading={loadingQuota}
              error={quotaError}
              saving={savingQuota}
              onLoadQuota={loadQuota}
              onSaveQuota={saveQuota}
              onConflict={handleConflict}
              onNotify={notify}
            />
          )}

          {activeTab === 'break-glass' && (
            <BreakGlassTabSection
              tenants={tenants}
              grants={grants}
              filterProjectId={grantFilterProjectId}
              onFilterProjectChange={setGrantFilterProjectId}
              loading={loadingGrants}
              error={grantsError}
              revokingGrantId={revokingGrantId}
              onLoadGrants={loadGrants}
              onIssueGrant={issueGrant}
              onRevokeGrant={revokeGrant}
              onConflict={handleConflict}
              onNotify={notify}
            />
          )}

          {activeTab === 'fonts' && (
            <PlatformFontsTabSection
              fonts={fonts}
              loading={loadingFonts}
              error={fontsError}
              uploading={uploadingFont}
              onLoadFonts={loadFonts}
              onUploadFont={uploadFont}
              onNotify={notify}
            />
          )}
        </div>
      </div>
    </div>
  );
};
