// booking-state.js

// Booking State Management

function getBooking() {
    return JSON.parse(sessionStorage.getItem('probook_booking')) || {};
}

function setBooking(partial) {
    const booking = getBooking();
    Object.assign(booking, partial);
    sessionStorage.setItem('probook_booking', JSON.stringify(booking));
}

function clearBooking() {
    sessionStorage.removeItem('probook_booking');
}

function syncBookingFromUrl(params) {
    // Assuming params is an object with keys corresponding to booking attributes
    setBooking(params);
}

function applyBookingToInputs({checkInId, checkOutId, roomNumberId, guestsId}) {
    const booking = getBooking();
    if (checkInId) document.getElementById(checkInId).value = booking.checkIn || '';
    if (checkOutId) document.getElementById(checkOutId).value = booking.checkOut || '';
    if (roomNumberId) document.getElementById(roomNumberId).value = booking.roomNumber || '';
    if (guestsId) document.getElementById(guestsId).value = booking.guests || 1;
}

// Function to manage pre-filling and restoring booking from session storage
function initBookingOnLoad() {
    applyBookingToInputs({
        checkInId: 'checkIn',  // Assuming these IDs correspond to your actual input fields
        checkOutId: 'checkOut',
        roomNumberId: 'roomNumber',
        guestsId: 'guests'
    });
}

// Attach initBookingOnLoad to window load event if needed
window.onload = initBookingOnLoad;