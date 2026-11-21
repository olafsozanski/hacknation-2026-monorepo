import { Button, type ButtonProps } from '@chakra-ui/react'
import { Check, Copy } from 'lucide-react'
import { useEffect, useState } from 'react'
import { notifyError } from '@/components/ui/toaster'

export function CopyButton({ text, label = 'Kopiuj', ...props }: { text: string; label?: string } & ButtonProps) {
  const [copied, setCopied] = useState(false)
  useEffect(() => {
    if (!copied) return
    const timer = window.setTimeout(() => setCopied(false), 2500)
    return () => window.clearTimeout(timer)
  }, [copied])

  const copy = async () => {
    try {
      await navigator.clipboard.writeText(text)
      setCopied(true)
    } catch (error) {
      notifyError(error, 'Nie udało się skopiować do schowka')
    }
  }

  return (
    <Button variant="outline" onClick={copy} aria-live="polite" {...props}>
      {copied ? <Check size={16} aria-hidden /> : <Copy size={16} aria-hidden />}
      {copied ? 'Skopiowano' : label}
    </Button>
  )
}
