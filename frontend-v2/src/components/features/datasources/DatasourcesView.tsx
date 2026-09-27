'use client';

import React, { useState, useEffect } from 'react';
import { Database } from 'lucide-react';
import { Tabs } from '../../ui';
import { useDataConnections } from '@/hooks/useDataConnections';
import { ConnectionsSection } from './ConnectionsSection';
import { DatasetsSection } from './DatasetsSection';

export const DatasourcesView: React.FC = () => {
  const [activeTab, setActiveTab] = useState<'connections' | 'datasets'>('connections');
  const { connections, fetchConnections } = useDataConnections();

  useEffect(() => {
    if (activeTab === 'datasets') fetchConnections();
  }, [activeTab, fetchConnections]);

  const tabs = [
    { id: 'connections', label: 'DataConnections', count: connections.length },
    { id: 'datasets',    label: 'Datasets' },
  ];

  return (
    <div className="h-full flex flex-col p-6 max-w-6xl mx-auto space-y-4">
      <div className="flex items-center gap-3 pb-3 border-b border-border">
        <div className="w-8 h-8 rounded-sm bg-slate-100 text-slate-600 flex items-center justify-center border border-slate-200 shrink-0">
          <Database className="w-4 h-4" />
        </div>
        <div>
          <p className="text-sm font-medium text-textPrimary">Data Sources</p>
          <p className="text-[11px] text-textMuted">DataConnection → Dataset → FieldMapping (SQL)</p>
        </div>
      </div>

      <Tabs
        tabs={tabs}
        activeTab={activeTab}
        onChange={id => setActiveTab(id as 'connections' | 'datasets')}
        className="-mx-6 px-6"
      />

      <div className="space-y-4">
        {activeTab === 'connections' && <ConnectionsSection />}
        {activeTab === 'datasets'    && <DatasetsSection connections={connections} />}
      </div>
    </div>
  );
};
