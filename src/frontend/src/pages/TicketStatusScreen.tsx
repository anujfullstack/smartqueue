import { useCallback, useEffect, useRef, useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { api, type TicketStatus } from '../api/client'
import { Button } from '../components/Button'
import { Card } from '../components/Card'
import { StatusBadge } from '../components/StatusBadge'
import { Dialog, DialogPanel, DialogTitle } from '@headlessui/react'

const POLL_INTERVAL_MS = 10_000

export function TicketStatusScreen() {
  const { ticketId, guestToken } = useParams<{ ticketId: string; guestToken: string }>()
  const navigate = useNavigate()

  const [ticket, setTicket] = useState<TicketStatus | null>(null)
  const [lastUpdated, setLastUpdated] = useState<Date | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [cancelOpen, setCancelOpen] = useState(false)
  const [cancelling, setCancelling] = useState(false)
  const intervalRef = useRef<ReturnType<typeof setInterval> | null>(null)

  const fetchStatus = useCallback(async () => {
    if (!ticketId || !guestToken) return
    try {
      const data = await api.getTicketStatus(ticketId, guestToken)
      setTicket(data)
      setLastUpdated(new Date())
      setError(null)
    } catch (err: any) {
      if (err.status === 403) setError('Invalid ticket link.')
      else if (err.status === 404) setError('Ticket not found.')
      else setError('Could not refresh status. Will retry.')
    }
  }, [ticketId, guestToken])

  useEffect(() => {
    fetchStatus()
    intervalRef.current = setInterval(fetchStatus, POLL_INTERVAL_MS)
    return () => { if (intervalRef.current) clearInterval(intervalRef.current) }
  }, [fetchStatus])

  async function handleCancel() {
    if (!ticketId || !guestToken) return
    setCancelling(true)
    try {
      await api.cancelTicket(ticketId, guestToken)
      setCancelOpen(false)
      await fetchStatus()
    } catch {
      setCancelOpen(false)
    } finally {
      setCancelling(false)
    }
  }

  const isDone = ticket && ['Completed', 'Cancelled', 'NoShow', 'Expired'].includes(ticket.status)

  return (
    <div className="min-h-screen bg-gray-50 px-4 py-10">
      <div className="mx-auto max-w-md">
        <div className="mb-6 text-center">
          <p className="text-sm font-medium text-indigo-600 uppercase tracking-wide">Your Queue Ticket</p>
          {ticket && <p className="text-5xl font-bold text-gray-900 mt-1">{ticket.ticketNumber}</p>}
        </div>

        {error && (
          <Card className="mb-4 border border-red-200 bg-red-50">
            <p className="text-sm text-red-700">{error}</p>
          </Card>
        )}

        {ticket && (
          <div className="space-y-4">
            <Card>
              <div className="flex items-center justify-between mb-4">
                <p className="text-sm text-gray-500">Status</p>
                <StatusBadge status={ticket.status} />
              </div>

              {!isDone && (
                <div className="grid grid-cols-2 gap-4">
                  <div className="rounded-xl bg-indigo-50 p-4 text-center">
                    <p className="text-3xl font-bold text-indigo-700">{ticket.position}</p>
                    <p className="mt-1 text-xs text-indigo-600">ahead of you</p>
                  </div>
                  <div className="rounded-xl bg-amber-50 p-4 text-center">
                    <p className="text-3xl font-bold text-amber-700">~{ticket.estimatedWaitMinutes}</p>
                    <p className="mt-1 text-xs text-amber-600">min wait</p>
                  </div>
                </div>
              )}

              {ticket.status === 'Called' && (
                <div className="mt-4 rounded-xl bg-purple-50 p-4 text-center">
                  <p className="text-lg font-semibold text-purple-800">It's your turn! 🎉</p>
                  <p className="text-sm text-purple-600 mt-1">Please make your way to the counter.</p>
                </div>
              )}

              {isDone && (
                <div className="mt-2 rounded-xl bg-gray-50 p-4 text-center">
                  <p className="text-sm text-gray-600">
                    {ticket.status === 'Completed' ? 'Thanks for visiting! See you soon.' :
                     ticket.status === 'Cancelled' ? 'Your ticket has been cancelled.' :
                     'This ticket is no longer active.'}
                  </p>
                  <button
                    onClick={() => navigate('/')}
                    className="mt-3 text-sm text-indigo-600 hover:underline"
                  >
                    Find another queue →
                  </button>
                </div>
              )}
            </Card>

            {lastUpdated && (
              <p className="text-center text-xs text-gray-400">
                Updated {lastUpdated.toLocaleTimeString()} · refreshes every 10s
              </p>
            )}

            {!isDone && (
              <Button
                variant="secondary"
                className="w-full"
                onClick={() => setCancelOpen(true)}
              >
                Cancel my spot
              </Button>
            )}
          </div>
        )}

        {/* Cancel confirmation dialog */}
        <Dialog open={cancelOpen} onClose={() => setCancelOpen(false)} className="relative z-50">
          <div className="fixed inset-0 bg-black/30" aria-hidden="true" />
          <div className="fixed inset-0 flex items-center justify-center p-4">
            <DialogPanel className="w-full max-w-sm rounded-2xl bg-white p-6 shadow-xl">
              <DialogTitle className="text-lg font-semibold text-gray-900">Cancel your spot?</DialogTitle>
              <p className="mt-2 text-sm text-gray-500">You'll lose your place in the queue and can't undo this.</p>
              <div className="mt-6 flex gap-3">
                <Button variant="secondary" className="flex-1" onClick={() => setCancelOpen(false)}>
                  Keep my spot
                </Button>
                <Button variant="danger" className="flex-1" loading={cancelling} onClick={handleCancel}>
                  Yes, cancel
                </Button>
              </div>
            </DialogPanel>
          </div>
        </Dialog>
      </div>
    </div>
  )
}
