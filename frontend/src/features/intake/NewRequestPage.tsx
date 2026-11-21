import { useState } from 'react'
import type { IntakeResult } from '@/api/types'
import { PageHeader } from '@/components/common/PageHeader'
import { useAnalyzeFile, useAnalyzeText } from './api'
import { AnalysisProgress } from './components/AnalysisProgress'
import { ReviewStep } from './components/ReviewStep'
import { SourceStep } from './components/SourceStep'

type Phase = { step: 'source' } | { step: 'review'; intake: IntakeResult | null }

export function NewRequestPage() {
  const [phase, setPhase] = useState<Phase>({ step: 'source' })
  const [fileName, setFileName] = useState<string>()
  const analyzeFile = useAnalyzeFile()
  const analyzeText = useAnalyzeText()
  const busy = analyzeFile.isPending || analyzeText.isPending
  const error = analyzeFile.error ?? analyzeText.error

  const toReview = (intake: IntakeResult) => setPhase({ step: 'review', intake })

  return (
    <>
      <PageHeader
        title="Rejestracja wniosku"
        breadcrumbs={[{ label: 'Rejestr wniosków', to: '/' }, { label: 'Rejestracja wniosku' }]}
        description={
          phase.step === 'source'
            ? 'Prześlij wniosek otrzymany e-mailem, przez ePUAP lub zeskanowany na dzienniku podawczym.'
            : 'Asystent wypełnił formularz na podstawie dokumentu. Zweryfikuj dane — decyzja należy do Ciebie.'
        }
      />
      {phase.step === 'review' ? (
        <ReviewStep intake={phase.intake} onRestart={() => setPhase({ step: 'source' })} />
      ) : busy ? (
        <AnalysisProgress fileName={fileName} />
      ) : (
        <SourceStep
          busy={busy}
          error={error?.message ?? null}
          onFile={(file) => {
            setFileName(file.name)
            analyzeFile.mutate(file, { onSuccess: toReview })
          }}
          onText={(text) => {
            setFileName(undefined)
            analyzeText.mutate(text, { onSuccess: toReview })
          }}
          onManual={() => setPhase({ step: 'review', intake: null })}
        />
      )}
    </>
  )
}
