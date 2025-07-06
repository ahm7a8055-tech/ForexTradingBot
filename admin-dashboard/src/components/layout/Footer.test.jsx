import React from 'react';
import { render, screen } from '@testing-library/react';
import Footer from './Footer'; // Adjust path as necessary

describe('Footer Component', () => {
  test('renders copyright notice', () => {
    render(<Footer />);
    const currentYear = new Date().getFullYear();
    // Using a regex to be flexible with surrounding text or minor changes
    const copyrightText = screen.getByText(/© \d{4} Admin Dashboard. All rights reserved./i);
    expect(copyrightText).toBeInTheDocument();
    expect(copyrightText.textContent).toContain(currentYear.toString());
  });

  test('matches snapshot', () => {
    const { asFragment } = render(<Footer />);
    expect(asFragment()).toMatchSnapshot();
  });
});
