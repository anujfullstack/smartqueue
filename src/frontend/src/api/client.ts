const BASE_URL = import.meta.env.VITE_API_BASE_URL ?? ''

async function request<T>(path: string, options?: RequestInit): Promise<T> {
  const res = await fetch(`${BASE_URL}${path}`, {
    headers: { 'Content-Type': 'application/json', ...options?.headers },
    ...options,
  })

  if (!res.ok) {
    const error = await res.json().catch(() => ({ message: 'Request failed' }))
    throw Object.assign(new Error(error.message ?? 'Request failed'), { status: res.status, body: error })
  }

  return res.json()
}

export interface LocationInfo {
  id: string
  name: string
  slug: string
  description?: string
  address?: string
  queues: { id: string; name: string; status: string }[]
}

export interface QueueStatus {
  queueId: string
  queueName: string
  status: string
  activeCount: number
  estimatedWaitMinutes: number
}

export interface JoinResult {
  ticketId: string
  ticketNumber: string
  guestToken: string
  position: number
  estimatedWaitMinutes: number
}

export interface TicketStatus {
  ticketId: string
  ticketNumber: string
  status: string
  position: number
  estimatedWaitMinutes: number
  queueStatus: string
  joinedAt: string
}

export interface ActiveTicket {
  id: string
  ticketNumber: string
  guestName: string
  partySize: number
  status: string
  position: number
  joinedAt: string
  waitingMinutes: number
}

export const api = {
  getLocation: (slug: string) => request<LocationInfo>(`/api/locations/${slug}`),

  getQueueStatus: (queueId: string) => request<QueueStatus>(`/api/queues/${queueId}/status`),

  joinQueue: (queueId: string, guestName: string, partySize: number) =>
    request<JoinResult>(`/api/queues/${queueId}/join`, {
      method: 'POST',
      body: JSON.stringify({ guestName, partySize }),
    }),

  getTicketStatus: (ticketId: string, guestToken: string) =>
    request<TicketStatus>(`/api/tickets/${ticketId}/${guestToken}`),

  cancelTicket: (ticketId: string, guestToken: string) =>
    request<void>(`/api/tickets/${ticketId}/${guestToken}/cancel`, { method: 'POST' }),

  // Staff endpoints
  getActiveTickets: (queueId: string, apiKey: string) =>
    request<ActiveTicket[]>(`/api/admin/queues/${queueId}/tickets`, {
      headers: { 'X-Api-Key': apiKey },
    }),

  advanceQueue: (queueId: string, apiKey: string) =>
    request<void>(`/api/queues/${queueId}/advance`, {
      method: 'POST',
      headers: { 'X-Api-Key': apiKey },
    }),

  pauseQueue: (queueId: string, apiKey: string) =>
    request<void>(`/api/queues/${queueId}/pause`, {
      method: 'POST',
      headers: { 'X-Api-Key': apiKey },
    }),

  closeQueue: (queueId: string, apiKey: string) =>
    request<void>(`/api/queues/${queueId}/close`, {
      method: 'POST',
      headers: { 'X-Api-Key': apiKey },
    }),

  reopenQueue: (queueId: string, apiKey: string) =>
    request<void>(`/api/queues/${queueId}/reopen`, {
      method: 'POST',
      headers: { 'X-Api-Key': apiKey },
    }),

  markNoShow: (ticketId: string, apiKey: string) =>
    request<void>(`/api/tickets/${ticketId}/no-show`, {
      method: 'POST',
      headers: { 'X-Api-Key': apiKey },
    }),

  completeTicket: (ticketId: string, apiKey: string) =>
    request<void>(`/api/tickets/${ticketId}/complete`, {
      method: 'POST',
      headers: { 'X-Api-Key': apiKey },
    }),
}
