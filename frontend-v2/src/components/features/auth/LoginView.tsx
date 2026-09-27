'use client';

import React, { useState } from 'react';
import { Eye, EyeOff, Lock, Mail, FileText } from 'lucide-react';
import { Input } from '@/components/ui/Input';
import { Button } from '@/components/ui/Button';
import { useAuth } from '@/hooks/useAuth';
import { loginRequestSchema } from '@/types/api';

export function LoginView() {
  const { login } = useAuth();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [showPassword, setShowPassword] = useState(false);
  const [errors, setErrors] = useState<{ email?: string; password?: string; general?: string }>({});
  const [isLoading, setIsLoading] = useState(false);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    setErrors({});

    const parsed = loginRequestSchema.safeParse({ email, password });
    if (!parsed.success) {
      const fieldErrors: typeof errors = {};
      for (const issue of parsed.error.issues) {
        const field = issue.path[0] as 'email' | 'password';
        fieldErrors[field] = issue.message;
      }
      setErrors(fieldErrors);
      return;
    }

    setIsLoading(true);
    try {
      await login(parsed.data);
      // AuthContext.login() sets state → AppContent re-renders to app view automatically
    } catch (err: any) {
      const msg = err?.message || 'เข้าสู่ระบบไม่สำเร็จ';
      if (err?.status === 401) {
        setErrors({ general: 'อีเมลหรือรหัสผ่านไม่ถูกต้อง' });
      } else {
        setErrors({ general: msg });
      }
    } finally {
      setIsLoading(false);
    }
  }

  return (
    <div className="min-h-screen bg-canvas flex items-center justify-center px-4">
      <div className="w-full max-w-sm">
        {/* Logo */}
        <div className="text-center mb-8">
          <div className="inline-flex items-center justify-center w-11 h-11 rounded-md bg-primary mb-3">
            <FileText className="w-5 h-5 text-white" />
          </div>
          <h1 className="text-base font-semibold text-textPrimary">SMK Document Server</h1>
          <p className="text-xs text-textMuted mt-0.5">เข้าสู่ระบบ Portal</p>
        </div>

        {/* Card */}
        <div className="bg-surface border border-border rounded-md p-6 space-y-4">
          {errors.general && (
            <div className="bg-red-50 border border-red-200 rounded-sm px-3 py-2">
              <p className="text-xs text-red-700">{errors.general}</p>
            </div>
          )}

          <form onSubmit={handleSubmit} className="space-y-4" noValidate>
            <Input
              label="อีเมล"
              type="email"
              placeholder="admin@sammakorn.co.th"
              value={email}
              onChange={(e) => setEmail(e.target.value)}
              error={errors.email}
              icon={Mail}
              autoComplete="email"
              autoFocus
            />

            <div className="space-y-1">
              <label className="block text-xs font-medium text-textSecondary">รหัสผ่าน</label>
              <div className="relative flex items-center">
                <Lock className="absolute left-2.5 w-3.5 h-3.5 text-textMuted pointer-events-none" />
                <input
                  type={showPassword ? 'text' : 'password'}
                  placeholder="••••••••"
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  autoComplete="current-password"
                  className={`w-full h-8 pl-8 pr-8 rounded-sm bg-surface border text-xs text-textPrimary placeholder:text-textMuted transition-colors focus:outline-none focus:border-primary focus:ring-1 focus:ring-primary ${
                    errors.password ? 'border-red-500 focus:border-red-500 focus:ring-red-500' : 'border-border'
                  }`}
                />
                <button
                  type="button"
                  onClick={() => setShowPassword((v) => !v)}
                  className="absolute right-2.5 text-textMuted hover:text-textSecondary transition-colors"
                  tabIndex={-1}
                  aria-label={showPassword ? 'ซ่อนรหัสผ่าน' : 'แสดงรหัสผ่าน'}
                >
                  {showPassword ? <EyeOff className="w-3.5 h-3.5" /> : <Eye className="w-3.5 h-3.5" />}
                </button>
              </div>
              {errors.password && (
                <p className="text-[11px] text-red-600 leading-none">{errors.password}</p>
              )}
            </div>

            <Button type="submit" className="w-full" loading={isLoading}>
              เข้าสู่ระบบ
            </Button>
          </form>
        </div>

        <p className="text-center text-[11px] text-textMuted mt-4">
          Sammakorn Document Platform v2
        </p>
      </div>
    </div>
  );
}
