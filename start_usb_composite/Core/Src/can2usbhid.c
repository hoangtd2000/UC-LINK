/*
 * can2usbhid.c
 *
 *  Created on: Aug 22, 2025
 *      Author: Hoang
 */

#include "can2usbhid.h"

CAN_RxHeaderTypeDef g_CanRxHeader;
static  uint8_t g_au8UsbFrame[HID_FRAME_SIZE] = {0};
HID_FrameFIFO_t g_HIDFrameFIFO_Tranfer;

void HAL_CAN_RxFifo0MsgPendingCallback(CAN_HandleTypeDef *hcan)
{
    //memset(g_au8UsbFrame, 0, sizeof(g_au8UsbFrame));
    if (HAL_CAN_GetRxMessage(hcan, CAN_RX_FIFO0, &g_CanRxHeader, &g_au8UsbFrame[6]) == HAL_OK)
    {
    	g_CanRxHeader.Timestamp = TIM5->CNT;
        // Byte 0: CMD
        g_au8UsbFrame[0] = 0x03;
        // Byte 1: DLC (4-bit high), FrameType (4-bit low)
        g_au8UsbFrame[1] = (g_CanRxHeader.DLC << 4) | (g_CanRxHeader.IDE & 0x0F);
        // Byte 2~5: CAN ID (big-endian)
        uint32_t l_u32CanID = (g_CanRxHeader.IDE == CAN_ID_STD) ? g_CanRxHeader.StdId : g_CanRxHeader.ExtId;
        g_au8UsbFrame[2] = (l_u32CanID >> 24) & 0xFF;
        g_au8UsbFrame[3] = (l_u32CanID >> 16) & 0xFF;
        g_au8UsbFrame[4] = (l_u32CanID >> 8) & 0xFF;
        g_au8UsbFrame[5] = l_u32CanID & 0xFF;

		g_au8UsbFrame[14] = (g_CanRxHeader.Timestamp >> 24) & 0xFF;
		g_au8UsbFrame[15] = (g_CanRxHeader.Timestamp >> 16) & 0xFF;
		g_au8UsbFrame[16]= (g_CanRxHeader.Timestamp >> 8) & 0xFF;
		g_au8UsbFrame[17] = (g_CanRxHeader.Timestamp ) & 0xFF;
        HID_Frame_Write(&g_HIDFrameFIFO_Tranfer,g_au8UsbFrame);
    }
}

uint8_t Can2Usb_Tranfer(HID_FrameFIFO_t *fifo, uint8_t *dest_buf)
{
    // Kiểm tra có frame không
    if(fifo->head == fifo->tail)
        return 0;  // FIFO rỗng

    // Copy frame ra buffer tạm
    memcpy(dest_buf, fifo->frame[fifo->tail], HID_FRAME_SIZE);

    // Thử gửi USB
    if(USBD_CUSTOM_HID_SendReport(&hUsbDevice, dest_buf, HID_FRAME_SIZE) == USBD_OK)
    {
        // Gửi thành công → đánh dấu frame đã đọc
        fifo->tail = (fifo->tail + 1) % HID_FRAME_BUFFER_SIZE;
        return 1;
    }
    else
    {
        // USB bận → không thay đổi tail, frame sẽ gửi lại lần sau
        return 2;  // Trạng thái gửi chưa thành công
    }
}
