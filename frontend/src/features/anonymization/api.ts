import { useMutation } from '@tanstack/react-query'
import { api, downloadFile } from '@/api/client'
import type { AnonymizationResult, Redaction } from '@/api/types'

export function useAnalyzeDocument() {
  return useMutation({
    mutationFn: (source: File | string) => {
      if (typeof source === 'string') {
        return api<AnonymizationResult>('/api/anonymization/analyze-text', { method: 'POST', body: { text: source } })
      }
      const form = new FormData()
      form.append('file', source)
      return api<AnonymizationResult>('/api/anonymization/analyze', { method: 'POST', body: form })
    },
  })
}

export function exportAnonymized(body: { content: string; redactions: Redaction[]; format: 'docx' | 'txt'; fileName: string | null }) {
  return downloadFile('/api/anonymization/export', `dokument-zanonimizowany.${body.format}`, body)
}
