import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { api, type LocationInfo } from '../api/client'
import { Button } from '../components/Button'
import { Card } from '../components/Card'
import { StatusBadge } from '../components/StatusBadge'

export function HomeScreen() {
  const [slug, setSlug] = useState('')
  const [location, setLocation] = useState<LocationInfo | null>(null)
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const navigate = useNavigate()

  async function handleFind(e: React.FormEvent) {
    e.preventDefault()
    if (!slug.trim()) return
    setLoading(true)
    setError(null)
    try {
      const data = await api.getLocation(slug.trim().toLowerCase())
      setLocation(data)
    } catch (err: any) {
      setError(err.status === 404 ? 'Location not found. Check the slug and try again.' : 'Something went wrong. Please try again.')
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="min-h-screen bg-gray-50 px-4 py-10">
      <div className="mx-auto max-w-md">
        <div className="mb-8 text-center">
          <div className="mb-3 text-4xl">☕</div>
          <h1 className="text-2xl font-bold text-gray-900">SmartQueue</h1>
          <p className="mt-1 text-sm text-gray-500">Skip the wait. Know when to arrive.</p>
        </div>

        <Card>
          <form onSubmit={handleFind} className="space-y-4">
            <div>
              <label htmlFor="slug" className="block text-sm font-medium text-gray-700">
                Venue code
              </label>
              <input
                id="slug"
                type="text"
                value={slug}
                onChange={e => setSlug(e.target.value)}
                placeholder="e.g. ovenfresh-main"
                className="mt-1 block w-full rounded-lg border border-gray-300 px-3 py-2.5 text-sm shadow-sm focus:border-indigo-500 focus:outline-none focus:ring-1 focus:ring-indigo-500"
              />
            </div>
            {error && <p className="text-sm text-red-600">{error}</p>}
            <Button type="submit" loading={loading} className="w-full" size="lg">
              Find Queue
            </Button>
          </form>
        </Card>

        {location && (
          <div className="mt-6 space-y-3">
            <div className="px-1">
              <h2 className="text-lg font-semibold text-gray-900">{location.name}</h2>
              {location.address && <p className="text-sm text-gray-500">{location.address}</p>}
            </div>

            {location.queues.length === 0 ? (
              <Card>
                <p className="text-center text-sm text-gray-500">No queues available right now.</p>
              </Card>
            ) : (
              location.queues.map(queue => (
                <Card key={queue.id} className="flex items-center justify-between gap-4">
                  <div>
                    <p className="font-medium text-gray-900">{queue.name}</p>
                    <StatusBadge status={queue.status} />
                  </div>
                  <Button
                    variant={queue.status === 'Open' ? 'primary' : 'secondary'}
                    disabled={queue.status !== 'Open'}
                    onClick={() => navigate(`/join/${queue.id}`)}
                  >
                    {queue.status === 'Open' ? 'Join' : queue.status}
                  </Button>
                </Card>
              ))
            )}
          </div>
        )}
      </div>
    </div>
  )
}
