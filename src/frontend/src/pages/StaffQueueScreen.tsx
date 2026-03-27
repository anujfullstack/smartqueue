import { useCallback, useEffect, useRef, useState } from 'react'
import { useParams } from 'react-router-dom'
import { api, type ActiveTicket, type QueueStatus } from '../api/client'
import { Button } from '../components/Button'
import { Card } from '../components/Card'
import { StatusBadge } from '../components/StatusBadge'
import { Dialog, DialogPanel, DialogTitle } from '@headlessui/react'

const STAFF_API_KEY = import.meta.env.VITE_STAFF_API_KEY ?? 'demo-staff-key-replace-in-prod'
const POLL_INTERVAL_MS = 15_000

export function StaffQueueScreen() {
  const { queueId } = useParams<{ queueId: string }>()

  const [tickets, setTickets] = useState<ActiveTicket[]>([])
  const [queueStatus, setQueueStatus] = useState<QueueStatus | null>(null)
  const [lastUpdated, setLastUpdated] = useState<Date | null>(null)
  const [actionLoading, setActionLoading] = useState<string | null>(null)
  const [closeDialogOpen, setCloseDialogOpen] = useState(false)
  const intervalRef = useRef<ReturnType<typeof setInterval> | null>(null)

  const refresh = useCallback(async () => {
    if (!queueId) return
    const [ticketData, statusData] = await Promise.allSettled([
      api.getActiveTickets(queueId, STAFF_API_KEY),
      api.getQueueStatus(queueId),
    ])
    if (ticketData.status === 'fulfilled') setTickets(ticketData.value)
    if (statusData.status === 'fulfilled') setQueueStatus(statusData.value)
    setLastUpdated(new Date())
  }, [queueId])

  useEffect(() => {
    refresh()
    intervalRef.current = setInterval(refresh, POLL_INTERVAL_MS)
    return () => { if (intervalRef.current) clearInterval(intervalRef.current) }
  }, [refresh])

  async function doAction(action: () => Promise<void>, key: string) {
    setActionLoading(key)
    try { await action() } finally {
      setActionLoading(null)
      await refresh()
    }
  }

  const isOpen = queueStatus?.status === 'Open'
  const isPaused = queueStatus?.status === 'Paused'

  return (
    <div className="min-h-screen bg-gray-50 px-4 py-6">
      <div className="mx-auto max-w-2xl">
        {/* Header */}
        <div className="mb-6 flex items-start justify-between gap-4">
          <div>
            <h1 className="text-xl font-bold text-gray-900">
              {queueStatus?.queueName ?? 'Queue'}
            </h1>
            <div className="mt-1 flex items-center gap-2">
              {queueStatus && <StatusBadge status={queueStatus.status} />}
              <span className="text-sm text-gray-500">
                {tickets.length} active · ~{queueStatus?.estimatedWaitMinutes ?? 0} min
              </span>
            </div>
          </div>
          <button onClick={refresh} className="text-sm text-indigo-600 hover:underline whitespace-nowrap">
            Refresh ↺
          </button>
        </div>

        {/* Queue controls */}
        <Card className="mb-4" padding="sm">
          <div className="flex flex-wrap gap-2">
            <Button
              size="sm"
              loading={actionLoading === 'advance'}
              disabled={!isOpen || tickets.length === 0}
              onClick={() => doAction(() => api.advanceQueue(queueId!, STAFF_API_KEY), 'advance')}
            >
              Call Next ›
            </Button>
            {isOpen && (
              <Button
                size="sm"
                variant="secondary"
                loading={actionLoading === 'pause'}
                onClick={() => doAction(() => api.pauseQueue(queueId!, STAFF_API_KEY), 'pause')}
              >
                Pause Queue
              </Button>
            )}
            {isPaused && (
              <Button
                size="sm"
                variant="secondary"
                loading={actionLoading === 'reopen'}
                onClick={() => doAction(() => api.reopenQueue(queueId!, STAFF_API_KEY), 'reopen')}
              >
                Reopen Queue
              </Button>
            )}
            {(isOpen || isPaused) && (
              <Button
                size="sm"
                variant="danger"
                onClick={() => setCloseDialogOpen(true)}
              >
                Close Queue
              </Button>
            )}
            {queueStatus?.status === 'Closed' && (
              <Button
                size="sm"
                variant="secondary"
                loading={actionLoading === 'reopen'}
                onClick={() => doAction(() => api.reopenQueue(queueId!, STAFF_API_KEY), 'reopen')}
              >
                Reopen Queue
              </Button>
            )}
          </div>
        </Card>

        {/* Ticket list */}
        {tickets.length === 0 ? (
          <Card>
            <p className="text-center text-sm text-gray-500 py-4">No active tickets right now.</p>
          </Card>
        ) : (
          <div className="space-y-3">
            {tickets.map((ticket, idx) => (
              <Card key={ticket.id} className={idx === 0 ? 'ring-2 ring-indigo-400' : ''}>
                <div className="flex items-center justify-between gap-3">
                  <div className="flex items-center gap-3">
                    <span className="text-2xl font-bold text-gray-800 w-14 shrink-0">{ticket.ticketNumber}</span>
                    <div>
                      <p className="font-medium text-gray-900">
                        {ticket.guestName}
                        {ticket.partySize > 1 && (
                          <span className="ml-1.5 text-xs text-gray-500">× {ticket.partySize}</span>
                        )}
                      </p>
                      <div className="flex items-center gap-2 mt-0.5">
                        <StatusBadge status={ticket.status} />
                        <span className="text-xs text-gray-400">{ticket.waitingMinutes}m waiting</span>
                      </div>
                    </div>
                  </div>

                  <div className="flex shrink-0 gap-1.5">
                    {ticket.status === 'Called' && (
                      <>
                        <Button
                          size="sm"
                          loading={actionLoading === `complete-${ticket.id}`}
                          onClick={() => doAction(() => api.completeTicket(ticket.id, STAFF_API_KEY), `complete-${ticket.id}`)}
                        >
                          Served ✓
                        </Button>
                        <Button
                          size="sm"
                          variant="secondary"
                          loading={actionLoading === `noshow-${ticket.id}`}
                          onClick={() => doAction(() => api.markNoShow(ticket.id, STAFF_API_KEY), `noshow-${ticket.id}`)}
                        >
                          No Show
                        </Button>
                      </>
                    )}
                  </div>
                </div>
              </Card>
            ))}
          </div>
        )}

        {lastUpdated && (
          <p className="mt-4 text-center text-xs text-gray-400">
            Updated {lastUpdated.toLocaleTimeString()} · auto-refreshes every 15s
          </p>
        )}
      </div>

      {/* Close queue confirmation */}
      <Dialog open={closeDialogOpen} onClose={() => setCloseDialogOpen(false)} className="relative z-50">
        <div className="fixed inset-0 bg-black/30" />
        <div className="fixed inset-0 flex items-center justify-center p-4">
          <DialogPanel className="w-full max-w-sm rounded-2xl bg-white p-6 shadow-xl">
            <DialogTitle className="text-lg font-semibold text-gray-900">Close the queue?</DialogTitle>
            <p className="mt-2 text-sm text-gray-500">
              No new guests can join until you reopen it. Active tickets are not affected.
            </p>
            <div className="mt-6 flex gap-3">
              <Button variant="secondary" className="flex-1" onClick={() => setCloseDialogOpen(false)}>
                Cancel
              </Button>
              <Button
                variant="danger"
                className="flex-1"
                loading={actionLoading === 'close'}
                onClick={() => {
                  setCloseDialogOpen(false)
                  doAction(() => api.closeQueue(queueId!, STAFF_API_KEY), 'close')
                }}
              >
                Close Queue
              </Button>
            </div>
          </DialogPanel>
        </div>
      </Dialog>
    </div>
  )
}
