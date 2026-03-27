import { type HTMLAttributes } from 'react'

interface CardProps extends HTMLAttributes<HTMLDivElement> {
  padding?: 'sm' | 'md' | 'lg'
}

const paddingClasses = { sm: 'p-3', md: 'p-4', lg: 'p-6' }

export function Card({ children, className = '', padding = 'md', ...rest }: CardProps) {
  return (
    <div
      {...rest}
      className={`rounded-2xl bg-white shadow-sm ring-1 ring-gray-200 ${paddingClasses[padding]} ${className}`}
    >
      {children}
    </div>
  )
}
