'use client';

import React, { useState } from 'react';
import { AppShell } from '@/components/layout/AppShell';
import { NavigationItemId, navLabels } from '@/components/layout/Sidebar';
import { TemplatesView } from '@/components/features/templates/TemplatesView';
import { UploadTemplateView } from '@/components/features/templates/UploadTemplateView';
import { TemplateStudioView } from '@/components/features/studio/TemplateStudioView';
import { GeneratorView } from '@/components/features/generator/GeneratorView';
import { AuditView } from '@/components/features/audit/AuditView';
import { LogsView } from '@/components/features/logs/LogsView';
import { FieldMappingView } from '@/components/features/mappings/FieldMappingView';
import { ApiKeysView } from '@/components/features/apikeys/ApiKeysView';
import { ApiDocsView } from '@/components/features/apidocs/ApiDocsView';
import { SettingsView } from '@/components/features/settings/SettingsView';
import { DatasourcesView } from '@/components/features/datasources/DatasourcesView';

export default function Home() {
  const [selectedStudioTemplateId, setSelectedStudioTemplateId] = useState<string | null>(null);
  const [selectedMappingTemplateId, setSelectedMappingTemplateId] = useState<string | null>(null);
  const [selectedGeneratorSlug, setSelectedGeneratorSlug] = useState<string>('');

  return (
    <AppShell initialTab="templates">
      {(activeTab, setActiveTab) => {
        switch (activeTab) {
          case 'templates':
            return (
              <TemplatesView
                onOpenStudio={(templateId) => {
                  setSelectedStudioTemplateId(templateId);
                  setActiveTab('studio');
                }}
                onOpenMapping={(templateId) => {
                  setSelectedMappingTemplateId(templateId);
                  setActiveTab('mapping');
                }}
                onOpenGenerator={(slug) => {
                  setSelectedGeneratorSlug(slug);
                  setActiveTab('generator');
                }}
                onNavigateUpload={() => setActiveTab('upload')}
              />
            );

          case 'upload':
            return (
              <UploadTemplateView
                onSuccess={(templateId) => {
                  setSelectedMappingTemplateId(templateId);
                  setActiveTab('mapping');
                }}
                onCancel={() => setActiveTab('templates')}
              />
            );

          case 'studio':
            return (
              <TemplateStudioView
                templateId={selectedStudioTemplateId || ''}
                onBack={() => setActiveTab('templates')}
              />
            );

          case 'generator':
            return <GeneratorView initialSlug={selectedGeneratorSlug} />;

          case 'audit':
            return <AuditView />;

          case 'logs':
            return <LogsView />;

          case 'mapping':
            return <FieldMappingView initialTemplateId={selectedMappingTemplateId || undefined} />;

          case 'projects':
            return <ApiKeysView />;

          case 'apidocs':
            return <ApiDocsView />;

          case 'settings':
            return <SettingsView />;

          case 'datasources':
            return <DatasourcesView />;

          case 'version-history':
          case 'users':
          case 'analytics':
            return (
              <div className="flex h-full items-center justify-center pt-20">
                <div className="text-center space-y-2">
                  <h3 className="text-sm font-bold text-textPrimary">
                    {navLabels[activeTab]} (Coming Soon)
                  </h3>
                  <p className="text-xs text-textMuted">ฟีเจอร์นี้อยู่ในระหว่างการพัฒนา</p>
                </div>
              </div>
            );

          default:
            return <TemplatesView onOpenStudio={() => {}} onOpenMapping={() => {}} onOpenGenerator={() => {}} />;
        }
      }}
    </AppShell>
  );
}
