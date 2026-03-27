import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom'
import { HomeScreen } from './pages/HomeScreen'
import { JoinQueueScreen } from './pages/JoinQueueScreen'
import { TicketStatusScreen } from './pages/TicketStatusScreen'
import { StaffQueueScreen } from './pages/StaffQueueScreen'

export default function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/" element={<HomeScreen />} />
        <Route path="/join/:queueId" element={<JoinQueueScreen />} />
        <Route path="/ticket/:ticketId/:guestToken" element={<TicketStatusScreen />} />
        <Route path="/staff/queues/:queueId" element={<StaffQueueScreen />} />
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </BrowserRouter>
  )
}
