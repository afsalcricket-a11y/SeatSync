import { useEffect, useState } from "react";
import axios from "axios";
import * as signalR from "@microsoft/signalr";

const API_BASE = "https://localhost:7089";
const HUB_PATH = "/hubs/seats";

export default function App() {
    const [token, setToken] = useState("");
    const [user, setUser] = useState(null);
    const [isLogin, setIsLogin] = useState(false);
    const [form, setForm] = useState({ fullName: "", email: "", password: "" });

    const [eventInfo, setEventInfo] = useState(null);
    const [seats, setSeats] = useState([]);
    const [selectedSeat, setSelectedSeat] = useState(null);
    const [statusMsg, setStatusMsg] = useState("");
    const [hubStatus, setHubStatus] = useState("Disconnected");

    const api = axios.create({
        baseURL: API_BASE,
        headers: token ? { Authorization: `Bearer ${token}` } : {},
    });

    const loadStadium = async () => {
        try {
            const evRes = await axios.get(`${API_BASE}/api/Events`);
            const firstEvent = evRes.data[0];
            if (!firstEvent) return;
            setEventInfo(firstEvent);

            const seatRes = await axios.get(`${API_BASE}/api/Events/${firstEvent.id}/seats`);
            setSeats(seatRes.data);
        } catch {
            setStatusMsg("Cannot reach API on https://localhost:7089. Make sure Visual Studio is running (F5).");
        }
    };

    const loadMyWallet = async (activeToken = token) => {
        if (!activeToken) return;
        try {
            const res = await axios.get(`${API_BASE}/api/Bookings/my`, {
                headers: { Authorization: `Bearer ${activeToken}` },
            });
            if (res.data.walletBalance !== undefined) {
                setUser((prev) => ({ ...prev, walletBalance: res.data.walletBalance }));
            }
        } catch {
            // Ignore if endpoint returns list directly
        }
    };

    useEffect(() => {
        loadStadium();
    }, []);

    useEffect(() => {
        const connection = new signalR.HubConnectionBuilder()
            .withUrl(`${API_BASE}${HUB_PATH}`)
            .withAutomaticReconnect()
            .build();

        connection.on("SeatStatusUpdated", (seatId, newStatus, lockedByUserId) => {
            setSeats((prev) =>
                prev.map((s) =>
                    s.id === seatId ? { ...s, status: newStatus, lockedByUserId } : s
                )
            );
        });

        connection
            .start()
            .then(() => setHubStatus("Live"))
            .catch(() => setHubStatus("Offline"));

        return () => {
            connection.stop();
        };
    }, []);

    const handleAuth = async (e) => {
        e.preventDefault();
        setStatusMsg("");
        try {
            const url = isLogin ? "/api/Auth/login" : "/api/Auth/register";
            const payload = isLogin
                ? { email: form.email, password: form.password }
                : form;
            const res = await axios.post(`${API_BASE}${url}`, payload);
            setToken(res.data.token);
            setUser({
                userId: res.data.userId,
                fullName: res.data.fullName,
                walletBalance: res.data.walletBalance,
            });
            setStatusMsg(`Signed in as ${res.data.fullName}`);
            loadMyWallet(res.data.token);
        } catch (err) {
            setStatusMsg(err.response?.data?.message || "Authentication failed.");
        }
    };

    const handleLockSeat = async (seat) => {
        if (!token) {
            setStatusMsg("Register or log in first to lock a seat.");
            return;
        }
        setStatusMsg("");
        try {
            await api.post("/api/Bookings/lock", { seatId: seat.id });
            setSelectedSeat(seat);
            setStatusMsg(`Locked seat ${seat.rowLabel}${seat.seatNumber} — click Confirm & Pay.`);
            loadStadium();
        } catch (err) {
            setStatusMsg(
                err.response?.status === 409
                    ? `409 Conflict: Seat ${seat.rowLabel}${seat.seatNumber} is already locked by another user!`
                    : err.response?.data?.message || "Could not lock seat."
            );
            loadStadium();
        }
    };

    const handleConfirmBooking = async () => {
        if (!selectedSeat) return;
        try {
            const idempotencyKey = `seat-${selectedSeat.id}-user-${user.userId}-${Date.now()}`;
            await api.post("/api/Bookings/confirm", {
                seatId: selectedSeat.id,
                idempotencyKey,
            });
            setStatusMsg(`Booked seat ${selectedSeat.rowLabel}${selectedSeat.seatNumber}!`);
            setSelectedSeat(null);
            await loadStadium();
            await loadMyWallet();
        } catch (err) {
            setStatusMsg(err.response?.data?.message || "Booking confirmation failed.");
        }
    };

    const getSeatColor = (status) => {
        const normalized = String(status).toLowerCase();
        if (normalized === "booked" || normalized === "2") return "#ef4444";
        if (normalized === "locked" || normalized === "1") return "#f59e0b";
        return "#22c55e";
    };

    return (
        <div style={{ fontFamily: "system-ui, sans-serif", maxWidth: 920, margin: "0 auto", padding: 24 }}>
            <header style={{ display: "flex", justifyContent: "space-between", alignItems: "center", borderBottom: "1px solid #ddd", paddingBottom: 12 }}>
                <div>
                    <h1 style={{ margin: 0 }}>SeatSync</h1>
                    <small style={{ color: "#555" }}>
                        {eventInfo ? `${eventInfo.title} • ${eventInfo.venue}` : "Loading match..."}
                    </small>
                </div>
                <span style={{ padding: "4px 10px", borderRadius: 12, fontSize: 13, background: hubStatus === "Live" ? "#dcfce7" : "#fee2e2", color: hubStatus === "Live" ? "#166534" : "#991b1b" }}>
                    SignalR: {hubStatus}
                </span>
            </header>

            {statusMsg && (
                <div style={{ marginTop: 16, padding: 12, borderRadius: 8, background: "#f1f5f9", borderLeft: "4px solid #2563eb", fontWeight: 500 }}>
                    {statusMsg}
                </div>
            )}

            {!user ? (
                <form onSubmit={handleAuth} style={{ marginTop: 20, display: "flex", gap: 10, flexWrap: "wrap" }}>
                    {!isLogin && (
                        <input
                            placeholder="Full Name"
                            value={form.fullName}
                            onChange={(e) => setForm({ ...form, fullName: e.target.value })}
                            required
                            style={{ padding: 8 }}
                        />
                    )}
                    <input
                        type="email"
                        placeholder="Email"
                        value={form.email}
                        onChange={(e) => setForm({ ...form, email: e.target.value })}
                        required
                        style={{ padding: 8 }}
                    />
                    <input
                        type="password"
                        placeholder="Password"
                        value={form.password}
                        onChange={(e) => setForm({ ...form, password: e.target.value })}
                        required
                        style={{ padding: 8 }}
                    />
                    <button type="submit" style={{ padding: "8px 16px", cursor: "pointer" }}>
                        {isLogin ? "Login" : "Register"}
                    </button>
                    <button type="button" onClick={() => setIsLogin(!isLogin)} style={{ padding: "8px 12px", cursor: "pointer" }}>
                        Switch to {isLogin ? "Register" : "Login"}
                    </button>
                </form>
            ) : (
                <div style={{ marginTop: 16, display: "flex", justifyContent: "space-between", alignItems: "center", background: "#f8fafc", padding: 12, borderRadius: 8 }}>
                    <div>
                        <strong>{user.fullName}</strong> (User #{user.userId}) — Wallet: <strong>₹{user.walletBalance}</strong>
                    </div>
                    {selectedSeat && (
                        <button
                            onClick={handleConfirmBooking}
                            style={{ background: "#2563eb", color: "#fff", border: "none", padding: "10px 18px", borderRadius: 6, cursor: "pointer", fontWeight: 600 }}
                        >
                            Confirm & Pay ₹{selectedSeat.price} ({selectedSeat.rowLabel}{selectedSeat.seatNumber})
                        </button>
                    )}
                </div>
            )}

            <div style={{ display: "flex", gap: 16, marginTop: 20, fontSize: 14 }}>
                <span><strong style={{ color: "#22c55e" }}>■</strong> Available</span>
                <span><strong style={{ color: "#f59e0b" }}>■</strong> Locked</span>
                <span><strong style={{ color: "#ef4444" }}>■</strong> Booked</span>
            </div>

            <div style={{ display: "grid", gridTemplateColumns: "repeat(10, 1fr)", gap: 10, marginTop: 16 }}>
                {seats.map((seat) => (
                    <button
                        key={seat.id}
                        onClick={() => handleLockSeat(seat)}
                        style={{
                            background: getSeatColor(seat.status),
                            color: "#fff",
                            border: selectedSeat?.id === seat.id ? "3px solid #1e3a8a" : "none",
                            borderRadius: 8,
                            padding: "14px 6px",
                            fontWeight: 700,
                            cursor: "pointer",
                        }}
                    >
                        {seat.rowLabel}{seat.seatNumber}
                    </button>
                ))}
            </div>
        </div>
    );
}