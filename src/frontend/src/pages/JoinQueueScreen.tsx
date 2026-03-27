import { useEffect, useState } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { api, type QueueStatus } from '../api/client'
import { Button } from '../components/Button'
import { Card } from '../components/Card'
import { StatusBadge } from '../components/StatusBadge'

export function JoinQueueScreen() {
  const { queueId } = useParams<{ queueId: string }>()
  const navigate = useNavigate()

  const [queueStatus, setQueueStatus] = useState<QueueStatus | null>(null)
  const [guestName, setGuestName] = useState('')
  const [partySize, setPartySize] = useState(1)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (!queueId) return
    api.getQueueStatus(queueId).then(setQueueStatus).catch(() => setError('Queue not found.'))
  }, [queueId])

  async function handleJoin(e: React.FormEvent) {
    e.preventDefault()
    if (!queueId || !guestName.trim()) return
    setLoading(true)
    setError(null)
    try {
      const result = await api.joinQueue(queueId, guestName.trim(), partySize)
      navigate(`/ticket/${result.ticketId}/${result.guestToken}`)
    } catch (err: any) {
      if (err.status === 409) setError('This queue is not accepting new guests right now.')
      else setError('Could not join the queue. Please try again.')
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="min-h-screen bg-gray-50 px-4 py-10">
      <div className="mx-auto max-w-md">
        <button
          onClick={() => navigate(-1)}
          className="mb-6 flex items-center gap-1 text-sm text-gray-500 hover:text-gray-700"
        >
          ← Back
        </button>

        {queueStatus && (
          <Card className="mb-4" padding="sm">
            <div className="flex items-center justify-between">
              <div>
                <p className="font-medium text-gray-900">{queueStatus.queueName}</p>
                <p className="text-sm text-gray-500">
                  {queueStatus.activeCount} ahead · ~{queueStatus.estimatedWaitMinutes} min wait
                </p>
              </div>
              <StatusBadge status={queueStatus.status} />
            </div>
          </Card>
        )}

        <Card>
          <h2 className="mb-4 text-lg font-semibold text-gray-900">Join the queue</h2>
          <form onSubmit={handleJoin} className="space-y-4">
            <div>
              <label className="block text-sm font-medium text-gray-700">Your name</label>
              <input
                type="text"
                value={guestName}
                onChange={e => setGuestName(e.target.value)}
                placeholder="Enter your name"
                required
                className="mt-1 block w-full rounded-lg border border-gray-300 px-3 py-2.5 text-sm shadow-sm focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
              />
            </div>

            <div>
              <label className="block text-sm font-medium text-gray-700">Party size</label>
              <div className="mt-1 flex items-center gap-3">
                <button
                  type="button"
                  onClick={() => setPartySize(s => Math.max(1, s - 1))}
                  className="flex h-9 w-9 items-center justify-center rounded-lg border border-gray-300 text-lg font-medium text-gray-700 hover:bg-gray-50"
                >
                  −
                </button>
                <span className="min-w-[2rem] text-center text-lg font-semibold">{partySize}</span>
                <button
                  type="button"
                  onClick={() => setPartySize(s => Math.min(20, s + 1))}
                  className="flex h-9 w-9 items-center justify-center rounded-lg border border-gray-300 text-lg font-medium text-gray-700 hover:bg-gray-50"
                >
                  +
                </button>
              </div>
            </div>

            {error && <p className="text-sm text-red-600">{error}</p>}

            <Button
              type="submit"
              loading={loading}
              disabled={queueStatus?.status !== 'Open'}
              className="w-full"
              size="lg"
            >
              Join Queue
            </Button>

            {queueStatus?.status !== 'Open' && (
              <p className="text-center text-sm text-gray-500">This queue is not open right now.</p>
            )}
          </form>
        </Card>
      </div>
    </div>
  )
}
