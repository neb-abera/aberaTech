//! `grader probe`: a health check for an image with no shell or curl.

use std::io::{Read, Write};
use std::net::{SocketAddr, TcpStream};
use std::time::Duration;

/// Whether `GET /healthz` at `address` answers 200 within two seconds.
#[must_use]
pub fn healthy(address: SocketAddr) -> bool {
    let timeout = Duration::from_secs(2);
    let Ok(mut stream) = TcpStream::connect_timeout(&address, timeout) else {
        return false;
    };
    if stream.set_read_timeout(Some(timeout)).is_err()
        || stream
            .write_all(b"GET /healthz HTTP/1.1\r\nHost: localhost\r\nConnection: close\r\n\r\n")
            .is_err()
    {
        return false;
    }
    let mut head = [0_u8; 12];
    stream.read_exact(&mut head).is_ok() && head.ends_with(b" 200")
}
