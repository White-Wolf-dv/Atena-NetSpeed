import sys
import psutil
from collections import deque
from PyQt6.QtWidgets import QApplication, QWidget
from PyQt6.QtCore import Qt, QTimer, QPoint, QRectF, QPointF
from PyQt6.QtGui import (
    QPainter, QColor, QFont, QPen, QLinearGradient, 
    QPainterPath, QPixmap
)

class NetworkOverlay(QWidget):
    def __init__(self):
        super().__init__()
        self.setWindowTitle("NetSpeed Overlay")

        self.setFixedSize(210, 110)


        self.setWindowFlags(
            Qt.WindowType.FramelessWindowHint |
            Qt.WindowType.WindowStaysOnTopHint |
            Qt.WindowType.Tool
        )
        self.setAttribute(Qt.WidgetAttribute.WA_TranslucentBackground)


        self.dl_speed = 0.0
        self.ul_speed = 0.0
        self.history_len = 30
        self.dl_history = deque([0.0] * self.history_len, maxlen=self.history_len)
        self.ul_history = deque([0.0] * self.history_len, maxlen=self.history_len)
        self.is_idle = False


        self.dl_text_rect = QRectF(8, 28, 92, 28)
        self.ul_text_rect = QRectF(108, 28, 92, 28)
        self.dl_glow_rect = self.dl_text_rect.translated(0, 1)
        self.ul_glow_rect = self.ul_text_rect.translated(0, 1)

        graph_left = 9.0
        graph_width = (self.width() - 9.0) - graph_left
        self.graph_bottom = self.height() - 10.0
        self.graph_height = 22.0
        step = graph_width / (self.history_len - 1)
        self.x_coords = [graph_left + i * step for i in range(self.history_len)]


        self.green_color = QColor(0, 255, 136)
        self.blue_color = QColor(0, 180, 255)
        self.green_glow = QColor(0, 255, 136, 65)
        self.blue_glow = QColor(0, 180, 255, 65)

        self.num_font = QFont("Segoe UI", 16, QFont.Weight.Bold)

        self.pen_dl_glow = QPen(QColor(0, 255, 136, 45), 3.0, Qt.PenStyle.SolidLine, Qt.PenCapStyle.RoundCap)
        self.pen_dl_main = QPen(self.green_color, 1.3, Qt.PenStyle.SolidLine, Qt.PenCapStyle.RoundCap)
        self.pen_ul_glow = QPen(QColor(0, 180, 255, 45), 3.0, Qt.PenStyle.SolidLine, Qt.PenCapStyle.RoundCap)
        self.pen_ul_main = QPen(self.blue_color, 1.3, Qt.PenStyle.SolidLine, Qt.PenCapStyle.RoundCap)


        self.static_bg = self._create_static_background()


        net_io = psutil.net_io_counters()
        self.last_rx = net_io.bytes_recv
        self.last_tx = net_io.bytes_sent


        self.timer = QTimer(self)
        self.timer.timeout.connect(self.update_speed)
        self.timer.start(1000)

        self.drag_pos = QPoint()

    def _create_static_background(self) -> QPixmap:

        pixmap = QPixmap(self.size())
        pixmap.fill(Qt.GlobalColor.transparent)

        painter = QPainter(pixmap)
        painter.setRenderHint(QPainter.RenderHint.Antialiasing)

        rect = QRectF(4, 4, self.width() - 8, self.height() - 8)
        radius = 12.0


        bg_gradient = QLinearGradient(rect.topLeft(), rect.bottomRight())
        bg_gradient.setColorAt(0, QColor(18, 28, 45, 220))
        bg_gradient.setColorAt(1, QColor(10, 16, 28, 238))

        painter.setPen(QPen(QColor(255, 255, 255, 45), 1.0))
        painter.setBrush(bg_gradient)
        painter.drawRoundedRect(rect, radius, radius)


        painter.setPen(QPen(QColor(255, 255, 255, 35), 1))
        painter.drawLine(int(self.width() / 2), 18, int(self.width() / 2), 68)


        text_white = QColor(235, 240, 250)
        unit_color = QColor(200, 210, 225)
        
        self._draw_arrow(painter, 28, 19, is_down=True, color=self.green_color)
        self._draw_arrow(painter, 126, 19, is_down=False, color=self.blue_color)

        painter.setFont(QFont("Segoe UI", 7, QFont.Weight.DemiBold))
        painter.setPen(text_white)
        painter.drawText(36, 23, "Download")
        painter.drawText(134, 23, "Upload")

        painter.setFont(QFont("Segoe UI", 6, QFont.Weight.Medium))
        painter.setPen(unit_color)
        painter.drawText(QRectF(8, 56, 92, 14), Qt.AlignmentFlag.AlignCenter, "MB/s")
        painter.drawText(QRectF(108, 56, 92, 14), Qt.AlignmentFlag.AlignCenter, "MB/s")


        self._draw_wifi_badge(painter, rect, self.green_color)


        painter.setPen(QPen(self.green_color, 1.5, Qt.PenStyle.SolidLine, Qt.PenCapStyle.RoundCap))
        painter.drawLine(int(rect.left() + 1), int(rect.bottom() - 28), int(rect.left() + 1), int(rect.bottom() - 11))
        
        painter.setPen(QPen(self.blue_color, 1.5, Qt.PenStyle.SolidLine, Qt.PenCapStyle.RoundCap))
        painter.drawLine(int(rect.right() - 1), int(rect.bottom() - 28), int(rect.right() - 1), int(rect.bottom() - 11))

        painter.end()
        return pixmap

    def _draw_arrow(self, painter, x, y, is_down, color):
        pen = QPen(color, 1.8, Qt.PenStyle.SolidLine, Qt.PenCapStyle.RoundCap, Qt.PenJoinStyle.RoundJoin)
        painter.setPen(pen)
        if is_down:
            painter.drawLine(x, y - 3, x, y + 4)
            painter.drawLine(x - 3, y + 1, x, y + 4)
            painter.drawLine(x + 3, y + 1, x, y + 4)
        else:
            painter.drawLine(x, y + 4, x, y - 3)
            painter.drawLine(x - 3, y, x, y - 3)
            painter.drawLine(x + 3, y, x, y - 3)

    def _draw_wifi_badge(self, painter, rect, color):
        badge_path = QPainterPath()
        bx, by = rect.right() - 24, rect.top()
        badge_path.moveTo(bx, by)
        badge_path.lineTo(rect.right() - 12, by)
        badge_path.arcTo(rect.right() - 24, by, 24, 24, 90, -90)
        badge_path.lineTo(rect.right(), by + 18)
        badge_path.lineTo(bx + 6, by + 18)
        badge_path.quadTo(bx, by + 18, bx, by + 12)
        badge_path.closeSubpath()

        painter.setPen(QPen(QColor(255, 255, 255, 30), 1))
        painter.setBrush(QColor(8, 14, 24, 180))
        painter.drawPath(badge_path)

        cx, cy = int(rect.right() - 11), int(rect.top() + 13)
        painter.setPen(QPen(color, 1.3, Qt.PenStyle.SolidLine, Qt.PenCapStyle.RoundCap))
        for r in (3, 6, 9):
            painter.drawArc(cx - r, cy - r, r * 2, r * 2, 45 * 16, 90 * 16)
        painter.setBrush(color)
        painter.drawEllipse(cx - 1, cy - 1, 2, 2)

    def update_speed(self):
        net_io = psutil.net_io_counters()
        rx = net_io.bytes_recv
        tx = net_io.bytes_sent

        dl = (rx - self.last_rx) / 1048576.0
        ul = (tx - self.last_tx) / 1048576.0

        self.last_rx = rx
        self.last_tx = tx

        self.dl_speed = dl
        self.ul_speed = ul
        self.dl_history.append(dl)
        self.ul_history.append(ul)

        if dl < 0.005 and ul < 0.005 and max(self.dl_history) < 0.005 and max(self.ul_history) < 0.005:
            if not self.is_idle:
                self.is_idle = True
                self.update()
            return

        self.is_idle = False
        self.update()

    def create_smooth_path(self, points):
        path = QPainterPath()
        path.moveTo(points[0])
        for i in range(len(points) - 1):
            p1 = points[i]
            p2 = points[i + 1]
            mid_x = (p1.x() + p2.x()) * 0.5
            path.cubicTo(QPointF(mid_x, p1.y()), QPointF(mid_x, p2.y()), p2)
        return path

    def paintEvent(self, event):
        painter = QPainter(self)
        

        painter.drawPixmap(0, 0, self.static_bg)

        painter.setRenderHint(QPainter.RenderHint.Antialiasing)
        painter.setFont(self.num_font)


        dl_str = f"{self.dl_speed:.2f}"
        ul_str = f"{self.ul_speed:.2f}"

        painter.setPen(self.green_glow)
        painter.drawText(self.dl_glow_rect, Qt.AlignmentFlag.AlignCenter, dl_str)
        painter.setPen(self.green_color)
        painter.drawText(self.dl_text_rect, Qt.AlignmentFlag.AlignCenter, dl_str)

        painter.setPen(self.blue_glow)
        painter.drawText(self.ul_glow_rect, Qt.AlignmentFlag.AlignCenter, ul_str)
        painter.setPen(self.blue_color)
        painter.drawText(self.ul_text_rect, Qt.AlignmentFlag.AlignCenter, ul_str)


        max_val = max(max(self.dl_history), max(self.ul_history), 0.5) * 1.2
        inv_max = 1.0 / max_val

        dl_base = self.graph_bottom - 6.0
        ul_base = self.graph_bottom - 1.0
        dl_scale = (self.graph_height - 7.0) * inv_max
        ul_scale = (self.graph_height - 9.0) * inv_max

        dl_points = [QPointF(self.x_coords[i], dl_base - self.dl_history[i] * dl_scale) for i in range(self.history_len)]
        ul_points = [QPointF(self.x_coords[i], ul_base - self.ul_history[i] * ul_scale) for i in range(self.history_len)]

        dl_path = self.create_smooth_path(dl_points)
        ul_path = self.create_smooth_path(ul_points)

        painter.setBrush(Qt.BrushStyle.NoBrush)
        
        painter.setPen(self.pen_dl_glow)
        painter.drawPath(dl_path)
        painter.setPen(self.pen_dl_main)
        painter.drawPath(dl_path)

        painter.setPen(self.pen_ul_glow)
        painter.drawPath(ul_path)
        painter.setPen(self.pen_ul_main)
        painter.drawPath(ul_path)

    def mousePressEvent(self, event):
        if event.button() == Qt.MouseButton.LeftButton:
            self.drag_pos = event.globalPosition().toPoint() - self.frameGeometry().topLeft()
            event.accept()

    def mouseMoveEvent(self, event):
        if event.buttons() == Qt.MouseButton.LeftButton:
            self.move(event.globalPosition().toPoint() - self.drag_pos)
            event.accept()

    def mouseDoubleClickEvent(self, event):
        if event.button() == Qt.MouseButton.RightButton:
            self.close()

if __name__ == "__main__":
    app = QApplication(sys.argv)
    overlay = NetworkOverlay()
    overlay.show()
    sys.exit(app.exec())