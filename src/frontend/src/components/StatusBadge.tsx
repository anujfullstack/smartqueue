type Status = string

const colorMap: Record<string, string> = {
  Open: 'bg-green-100 text-green-800',
  Paused: 'bg-yellow-100 text-yellow-800',
  Closed: 'bg-red-100 text-red-800',
  Waiting: 'bg-blue-100 text-blue-800',
  Called: 'bg-purple-100 text-purple-800',
  InService: 'bg-indigo-100 text-indigo-800',
  Completed: 'bg-gray-100 text-gray-600',
  Cancelled: 'bg-gray-100 text-gray-500',
  NoShow: 'bg-orange-100 text-orange-700',
}

export function StatusBadge({ status }: { status: Status }) {
  const colors = colorMap[status] ?? 'bg-gray-100 text-gray-600'
  return (
    <span className={`inline-flex items-center rounded-full px-2.5 py-0.5 text-xs font-medium ${colors}`}>
      {status}
    </span>
  )
}
