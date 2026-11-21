export type ProblemDetails = {
  title?: string
  detail?: string
  status?: number
  type?: string
  errors?: Record<string, string[]>
}

export class ApiError extends Error {
  readonly status: number
  readonly problem: ProblemDetails

  constructor(status: number, problem: ProblemDetails) {
    super(problem.detail ?? defaultMessage(status))
    this.status = status
    this.problem = problem
  }

  get fieldErrors(): Record<string, string> {
    return Object.fromEntries(
      Object.entries(this.problem.errors ?? {}).map(([field, messages]) => [field, messages.join(' ')]),
    )
  }
}

function defaultMessage(status: number) {
  if (status === 400) return 'Sprawdź poprawność wprowadzonych danych.'
  if (status === 401) return 'Sesja wygasła. Zaloguj się ponownie.'
  if (status === 403) return 'Nie masz uprawnień do wykonania tej operacji.'
  if (status === 404) return 'Nie znaleziono zasobu.'
  if (status === 409) return 'Dane zostały zmienione przez innego użytkownika. Odśwież stronę.'
  if (status === 413) return 'Plik jest za duży.'
  if (status === 429) return 'Zbyt wiele żądań. Odczekaj chwilę i spróbuj ponownie.'
  if (status >= 500) return 'Wystąpił błąd serwera. Spróbuj ponownie za chwilę.'
  return 'Nie udało się wykonać operacji.'
}

let csrfToken: string | null = null
let unauthorizedHandler: () => void = () => {}

export function setCsrfToken(token: string) {
  csrfToken = token
}

export function onUnauthorized(handler: () => void) {
  unauthorizedHandler = handler
}

type RequestOptions = {
  method?: 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE'
  body?: unknown
  signal?: AbortSignal
  accept?: string
}

async function send(path: string, { method = 'GET', body, signal, accept = 'application/json' }: RequestOptions): Promise<Response> {
  const headers: Record<string, string> = { Accept: accept }
  if (method !== 'GET' && csrfToken) headers['X-XSRF-TOKEN'] = csrfToken
  let payload: BodyInit | undefined
  if (body instanceof FormData) {
    payload = body
  } else if (body !== undefined) {
    headers['Content-Type'] = 'application/json'
    payload = JSON.stringify(body)
  }

  let response: Response
  try {
    response = await fetch(path, { method, headers, body: payload, signal, credentials: 'same-origin' })
  } catch (error) {
    if (error instanceof DOMException && error.name === 'AbortError') throw error
    throw new ApiError(0, { title: 'Brak połączenia z serwerem. Sprawdź sieć i spróbuj ponownie.' })
  }

  if (!response.ok) {
    if (response.status === 401 && !path.startsWith('/api/auth/')) unauthorizedHandler()
    throw new ApiError(response.status, await readProblem(response))
  }
  return response
}

async function readProblem(response: Response): Promise<ProblemDetails> {
  const type = response.headers.get('Content-Type') ?? ''
  if (type.includes('json')) {
    try {
      return (await response.json()) as ProblemDetails
    } catch {
      return {}
    }
  }
  return {}
}

export async function api<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const response = await send(path, options)
  if (response.status === 204 || response.headers.get('Content-Length') === '0') return undefined as T
  const type = response.headers.get('Content-Type') ?? ''
  return (type.includes('json') ? await response.json() : undefined) as T
}

export function postEventStream(path: string, body: unknown, signal: AbortSignal): Promise<Response> {
  return send(path, { method: 'POST', body, signal, accept: 'text/event-stream, application/problem+json' })
}

export async function downloadFile(path: string, fallbackName: string, body?: unknown) {
  const response = await send(path, body === undefined ? {} : { method: 'POST', body })
  const blob = await response.blob()
  const name = fileNameFrom(response.headers.get('Content-Disposition')) ?? fallbackName
  const url = URL.createObjectURL(blob)
  const link = document.createElement('a')
  link.href = url
  link.download = name
  document.body.append(link)
  link.click()
  link.remove()
  URL.revokeObjectURL(url)
}

function fileNameFrom(disposition: string | null) {
  if (!disposition) return null
  const encoded = /filename\*=UTF-8''([^;]+)/i.exec(disposition)
  if (encoded) return decodeURIComponent(encoded[1])
  const plain = /filename="?([^";]+)"?/i.exec(disposition)
  return plain ? plain[1] : null
}

export function query(params: Record<string, string | number | boolean | null | undefined>) {
  const search = new URLSearchParams()
  for (const [key, value] of Object.entries(params)) {
    if (value !== undefined && value !== null && value !== '') search.set(key, String(value))
  }
  const text = search.toString()
  return text ? `?${text}` : ''
}
