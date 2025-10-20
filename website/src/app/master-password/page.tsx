"use client";

import { generateMasterPassword } from "@/lib/crypto";
import { useState } from "react";
import { Copy, Eye, EyeOff, Key, Lock, Shield } from "lucide-react";
import { Input } from "@/components/ui/input";

export default function MasterPasswordGenerator() {
  const [baseSecret, setBaseSecret] = useState("");
  const [macAddress, setMacAddress] = useState("");
  const [generatedPassword, setGeneratedPassword] = useState("");
  const [nonce, setNonce] = useState("");
  const [isGenerating, setIsGenerating] = useState(false);
  const [error, setError] = useState("");
  const [copied, setCopied] = useState(false);
  const [showBaseSecret, setShowBaseSecret] = useState(false);  

  const handleGenerate = async () => {
    setError("");
    setGeneratedPassword("");
    setNonce("");
    setCopied(false);

    // Validation
    if (!baseSecret.trim()) {
      setError("Please enter the base secret");
      return;
    }

    if (!macAddress.trim()) {
      setError("Please enter the kiosk MAC address");
      return;
    }

    // Validate MAC address format
    const macPattern = /^([0-9A-F]{2}[:-]){5}([0-9A-F]{2})$/i;
    if (!macPattern.test(macAddress.trim())) {
      setError("Invalid MAC address format. Use XX:XX:XX:XX:XX:XX or XX-XX-XX-XX-XX-XX");
      return;
    }

    setIsGenerating(true);

    try {
      const result = await generateMasterPassword(
        baseSecret.trim(),
        macAddress.trim()
      );
      setGeneratedPassword(result.password);
      setNonce(result.nonce);
    } catch (err) {
      setError(err instanceof Error ? err.message : "Failed to generate password");
    } finally {
      setIsGenerating(false);
    }
  };

  const copyToClipboard = async () => {
    if (!generatedPassword) return;

    try {
      await navigator.clipboard.writeText(generatedPassword);
      setCopied(true);
      setTimeout(() => setCopied(false), 2000);
    } catch (err) {
      setError("Failed to copy to clipboard");
    }
  };

  return (
    <div className="min-h-screen bg-gradient-to-br from-slate-50 to-slate-100 dark:from-slate-900 dark:to-slate-800 flex items-center justify-center p-4">
      <div className="w-full max-w-md">
        {/* Header */}
        <div className="text-center mb-8">
          <div className="inline-flex items-center justify-center w-16 h-16 bg-blue-500 rounded-full mb-4">
            <Shield className="w-8 h-8 text-white" />
          </div>
          <h1 className="text-3xl font-bold text-slate-900 dark:text-white mb-2">
            Master Password Generator
          </h1>
          <p className="text-slate-600 dark:text-slate-400">
            PhotoBoothX Support Tool
          </p>
        </div>

        {/* Card */}
        <div className="bg-white dark:bg-slate-800 rounded-lg shadow-lg p-6 space-y-6">
          {/* Base Secret Input */}
          <div>
            <label htmlFor="baseSecret" className="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-2">
              <div className="flex items-center gap-2">
                <Key className="w-4 h-4" />
                Base Secret
              </div>
            </label>
            <div className="flex items-center gap-2 relative">
            <Input
              id="baseSecret"
              type={showBaseSecret ? "text" : "password"}
              value={baseSecret}
              onChange={(e) => setBaseSecret(e.target.value)}
              onKeyDown={(e) => e.key === "Enter" && handleGenerate()}
              placeholder="Enter base secret"
              className="h-12 outline-none pr-10 focus:ring-0 focus:ring-offset-0 focus:border-transparent bg-transparent focus:outline-none focus-visible:ring-0 focus-visible:ring-offset-0"
            />
            {showBaseSecret ? 
            <Eye onClick={() => setShowBaseSecret(!showBaseSecret)} className="w-4 h-4 absolute right-2 top-1/2 -translate-y-1/2 text-slate-500 hover:text-slate-600 dark:hover:text-slate-400" /> : <EyeOff onClick={() => setShowBaseSecret(!showBaseSecret)} className="w-4 h-4 absolute right-2 top-1/2 -translate-y-1/2 text-slate-500 hover:text-slate-600 dark:hover:text-slate-400" />}
            </div>
          </div>

          {/* MAC Address Input */}
          <div>
            <label htmlFor="macAddress" className="block text-sm font-medium text-slate-700 dark:text-slate-300 mb-2">
              <div className="flex items-center gap-2">
                <Lock className="w-4 h-4" />
                Kiosk MAC Address
              </div>
            </label>
            <Input
              id="macAddress"
              type="text"
              value={macAddress}
              onChange={(e) => setMacAddress(e.target.value.toUpperCase())}
              onKeyDown={(e) => e.key === "Enter" && handleGenerate()}
              placeholder="00:1A:2B:3C:4D:5E"
              className="h-12 outline-none pr-10 focus:ring-0 focus:ring-offset-0 focus:border-transparent bg-transparent focus:outline-none focus-visible:ring-0 focus-visible:ring-offset-0"
            />
            <p className="text-xs text-slate-500 dark:text-slate-400 mt-1">
              Format: XX:XX:XX:XX:XX:XX or XX-XX-XX-XX-XX-XX
            </p>
          </div>

          {/* Error Message */}
          {error && (
            <div className="p-3 bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800 rounded-lg">
              <p className="text-sm text-red-600 dark:text-red-400">{error}</p>
            </div>
          )}

          {/* Generate Button */}
          <button
            onClick={handleGenerate}
            disabled={isGenerating}
            className="w-full bg-blue-500 hover:bg-blue-600 disabled:bg-slate-300 dark:disabled:bg-slate-600 text-white font-medium py-3 px-4 rounded-lg transition-colors disabled:cursor-not-allowed"
          >
            {isGenerating ? "Generating..." : "Generate Password"}
          </button>

          {/* Generated Password Display */}
          {generatedPassword && (
            <div className="p-4 bg-green-50 dark:bg-green-900/20 border-2 border-green-200 dark:border-green-800 rounded-lg space-y-3">
              <div className="flex items-center justify-between">
                <span className="text-sm font-medium text-green-700 dark:text-green-300">
                  Master Password
                </span>
                <button
                  onClick={copyToClipboard}
                  className="flex items-center gap-1 text-sm text-green-600 dark:text-green-400 hover:text-green-700 dark:hover:text-green-300"
                >
                  <Copy className="w-4 h-4" />
                  {copied ? "Copied!" : "Copy"}
                </button>
              </div>
              <div className="font-mono text-3xl font-bold text-green-900 dark:text-green-100 tracking-wider text-center">
                {generatedPassword.substring(0, 4)}-{generatedPassword.substring(4, 8)}
              </div>
              <div className="text-xs text-green-600 dark:text-green-400 space-y-1">
                <p>• Single-use only (expires after first login)</p>
                <p>• Works with any admin username</p>
                <p>• Nonce: {nonce}</p>
              </div>
            </div>
          )}

          {/* Instructions */}
          <div className="pt-4 border-t border-slate-200 dark:border-slate-700">
            <h3 className="text-sm font-medium text-slate-700 dark:text-slate-300 mb-2">
              Instructions:
            </h3>
            <ol className="text-xs text-slate-600 dark:text-slate-400 space-y-1 list-decimal list-inside">
              <li>Enter the base secret (shared with all kiosks)</li>
              <li>Enter the MAC address of the target kiosk</li>
              <li>Click "Generate Password"</li>
              <li>Provide the 8-digit password to the admin</li>
              <li>Admin enters their username + master password</li>
            </ol>
          </div>
        </div>

        {/* Security Notice */}
        <div className="mt-6 p-4 bg-amber-50 dark:bg-amber-900/20 border border-amber-200 dark:border-amber-800 rounded-lg">
          <p className="text-xs text-amber-700 dark:text-amber-300">
            <strong>Security Notice:</strong> Keep the base secret secure. Each password can only be used once and is specific to the kiosk's MAC address.
          </p>
        </div>
      </div>
    </div>
  );
}

